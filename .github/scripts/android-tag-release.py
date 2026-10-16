"""Read-only release gates: immutable dev tag -> protected PR -> exact main merge."""
import argparse
import io
import json
import os
from pathlib import Path
import re
import subprocess
import zipfile

REPOSITORY = 'Sparta-fourTeam/client'
WORKFLOW_ID = 378016847
# Release infrastructure must be synchronized through review before tagging dev.
PROTECTED_PATHS = ('.github', 'docs/android-release.md',
                   'Assets/Settings/Build Profiles/Android_Release.asset',
                   'Assets/_Project/Scripts/Editor/CiBuild.cs')


def git(*args):
    return subprocess.check_output(['git', *args], text=True).strip()


def api(endpoint, paginate=False, binary=False):
    args = ['gh', 'api', f'repos/{REPOSITORY}/{endpoint}']
    if paginate:
        args += ['--paginate', '--slurp']
    value = subprocess.check_output(args)
    return value if binary else json.loads(value)


def version_from_tag(tag):
    if not re.fullmatch(r'v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)', tag):
        raise ValueError('Release tag must be vMAJOR.MINOR.PATCH without leading zeroes')
    return tag[1:]


def check_scope(event, env):
    if env.get('GITHUB_REPOSITORY') != REPOSITORY or event['repository']['full_name'] != REPOSITORY:
        raise ValueError('Only the original release repository is allowed')
    if env.get('GITHUB_RUN_ATTEMPT') != '1':
        raise ValueError('Re-run is disabled; use a new version tag and release PR')


def tag_identity(tag):
    version_from_tag(tag)
    ref = 'refs/tags/' + tag
    obj, source = git('rev-parse', ref), git('rev-parse', ref + '^{commit}')
    # checkout intentionally removes Git credentials. Use the existing read-only
    # GH_TOKEN transport so this also works for private repositories.
    live = api('git/ref/tags/' + tag)
    if live.get('ref') != ref or live.get('object', {}).get('sha') != obj:
        raise ValueError('Tag moved or disappeared; never move or reuse a release tag')
    subprocess.run(['git', 'merge-base', '--is-ancestor', source, 'origin/dev'], check=True)
    return obj, source


def preserve_release_code(base, source):
    result = subprocess.run(['git', 'diff', '--quiet', base, source, '--', *PROTECTED_PATHS])
    if result.returncode:
        raise ValueError('Tagged dev source must preserve current main release infrastructure; synchronize through review first')


def merge_tree(base, source):
    result = subprocess.run(['git', 'merge-tree', '--write-tree', base, source], text=True, capture_output=True)
    if result.returncode:
        raise ValueError('Release source conflicts with main; resolve on dev before creating a new tag')
    return result.stdout.splitlines()[0]


def tag_run(tag, source, current_run=None):
    pages = api(f'actions/workflows/{WORKFLOW_ID}/runs?per_page=100', paginate=True)
    runs = [r for p in pages for r in p['workflow_runs'] if r['event'] == 'push' and r['head_branch'] == tag]
    if len(runs) != 1:
        raise ValueError('Release tag has no unique first push; moved/recreated/reused tags are rejected')
    run = runs[0]
    if (run['workflow_id'] != WORKFLOW_ID or run['head_sha'] != source or run['run_attempt'] != 1 or
            run['repository']['full_name'] != REPOSITORY):
        raise ValueError('Tag request run identity mismatch')
    if current_run is not None:
        if run['id'] != current_run:
            raise ValueError('A prior request already used this release tag')
    elif run['status'] != 'completed' or run['conclusion'] != 'success':
        raise ValueError('Tag preflight must finish successfully before merging the release PR')
    return run


def prepare_request(event, env):
    check_scope(event, env)
    if (env['GITHUB_EVENT_NAME'] != 'push' or not event.get('created') or event.get('deleted') or
            event.get('forced') or event.get('before') != '0' * 40):
        raise ValueError('Only a newly created release tag push is allowed')
    if env.get('GITHUB_REF_PROTECTED') != 'true':
        raise ValueError('Release tag protection is required before use (prevent updates/deletion; no bypass)')
    tag = env['GITHUB_REF'].removeprefix('refs/tags/')
    if event['ref'] != 'refs/tags/' + tag:
        raise ValueError('Tag event ref mismatch')
    obj, source = tag_identity(tag)
    if event['after'] not in (obj, source) or env['GITHUB_SHA'] != source:
        raise ValueError('Tag event source mismatch')
    base = git('rev-parse', 'origin/main')
    preserve_release_code(base, source)
    merge_tree(base, source)
    run = tag_run(tag, source, int(env['GITHUB_RUN_ID']))
    return dict(schema=1, tag=tag, version_name=version_from_tag(tag), tag_object_sha=obj,
                tag_source_sha=source, main_base_sha=base, tag_run_id=run['id'],
                workflow_id=WORKFLOW_ID, protected_tag=True)


def load_request(tag, source):
    run = tag_run(tag, source)
    pages = api(f'actions/runs/{run["id"]}/artifacts?per_page=100', paginate=True)
    artifacts = [a for p in pages for a in p['artifacts'] if a['name'] == 'tag-release-request']
    if len(artifacts) != 1 or artifacts[0]['expired']:
        raise ValueError('Original tag request artifact is missing or expired; use a new tag')
    archive = api(f'actions/artifacts/{artifacts[0]["id"]}/zip', binary=True)
    with zipfile.ZipFile(io.BytesIO(archive)) as zipped:
        if zipped.namelist() != ['tag-release-request.json'] or zipped.getinfo(zipped.namelist()[0]).file_size > 16384:
            raise ValueError('Unexpected tag request archive')
        request = json.loads(zipped.read('tag-release-request.json'))
    obj, _ = tag_identity(tag)
    if (request.get('schema') != 1 or request.get('tag') != tag or request.get('tag_source_sha') != source or
            request.get('tag_object_sha') != obj or request.get('tag_run_id') != run['id'] or
            request.get('workflow_id') != WORKFLOW_ID or request.get('protected_tag') is not True or
            request.get('version_name') != version_from_tag(tag)):
        raise ValueError('Original tag request no longer matches the release')
    if not re.fullmatch(r'[0-9a-f]{40}', request.get('main_base_sha', '')):
        raise ValueError('Invalid original main base SHA')
    return request


def pr_tag(event):
    pr = event['pull_request']
    if (pr['base']['ref'] != 'main' or pr['base']['repo']['full_name'] != REPOSITORY or
            pr['head']['repo']['full_name'] != REPOSITORY or not pr['head']['ref'].startswith('release/v')):
        raise ValueError('Release PR must be release/vMAJOR.MINOR.PATCH -> main in the original repository')
    tag = pr['head']['ref'].removeprefix('release/')
    version_from_tag(tag)
    return tag


def validate_pr(event, env):
    check_scope(event, env)
    if env['GITHUB_EVENT_NAME'] != 'pull_request':
        raise ValueError('A protected release PR event is required')
    tag = pr_tag(event)
    obj, source = tag_identity(tag)
    pr = event['pull_request']
    if pr['head']['sha'] != source:
        raise ValueError('Release PR head differs from tag source (newer dev commits must not be mixed in)')
    request = load_request(tag, source)
    base = request['main_base_sha']
    preserve_release_code(base, source)
    expected_tree = merge_tree(base, source)
    if event['action'] == 'closed':
        merged = pr['merge_commit_sha']
        if not pr['merged'] or env.get('GITHUB_REF') != 'refs/heads/main' or env['GITHUB_SHA'] != merged:
            raise ValueError('Only the actual protected main merge may deploy')
        if git('rev-parse', 'HEAD') != merged or git('rev-parse', 'origin/main') != merged:
            raise ValueError('Checkout/current main differs from this release merge')
        parents = git('rev-list', '--parents', '-n', '1', merged).split()
        if parents != [merged, base, source] or git('rev-parse', merged + '^{tree}') != expected_tree:
            raise ValueError('Release requires a plain merge commit of the exact base and tag; squash/rebase/extra commits are rejected')
        request = dict(request, source_commit=merged, merge_commit_sha=merged, pr_number=pr['number'],
                       integrated_tree=expected_tree)
    elif event['action'] in ('opened', 'synchronize', 'reopened'):
        if git('rev-parse', 'origin/main') != base or pr['base']['sha'] != base:
            raise ValueError('Main advanced since tag preflight; synchronize dev and use a new tag')
    else:
        raise ValueError('Unsupported release PR event')
    return request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['request', 'pr', 'merged'])
    args = parser.parse_args()
    env = os.environ
    event = json.loads(Path(env['GITHUB_EVENT_PATH']).read_text())
    if args.mode == 'request':
        request = prepare_request(event, env)
    else:
        if (args.mode == 'merged') != (event['action'] == 'closed'):
            raise ValueError('Release gate mode does not match event')
        request = validate_pr(event, env)
    Path('build').mkdir(exist_ok=True)
    name = 'tag-release-request.json' if args.mode == 'request' else 'tag-release.json'
    Path('build', name).write_text(json.dumps(request, indent=2) + '\n')
    with open(env['GITHUB_OUTPUT'], 'a') as out:
        out.write(f'tag={request["tag"]}\nversion_name={request["version_name"]}\n')
    with open(env['GITHUB_STEP_SUMMARY'], 'a') as out:
        out.write(f'### Tag release {request["tag"]}\n\nTag source: `{request["tag_source_sha"]}`  \n'
                  f'Main base: `{request["main_base_sha"]}`  \nTag request run: `{request["tag_run_id"]}`\n\n')
        if args.mode == 'request':
            out.write('Read-only preflight passed. The dedicated integration job will create the fixed release PR, '
                      'request latest-SHA Codex review and use GitHub protected merge before building.\n')
        elif args.mode == 'merged':
            out.write(f'Merged/build source: `{request["merge_commit_sha"]}`  \nPR: #{request["pr_number"]}  \n'
                      f'Integrated tree: `{request["integrated_tree"]}`\n')


if __name__ == '__main__':
    main()
