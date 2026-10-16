"""Build an immutable dev tag directly; no release PR, review request or merge."""
import argparse
import importlib.util
import io
import json
import os
from pathlib import Path
import re
import zipfile

spec = importlib.util.spec_from_file_location('tag_policy', Path(__file__).with_name('android-tag-release.py'))
tag = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tag)
MODE = 'tagged-dev-v1'
RETRY_POLICY = 'before-build-v1'


def artifact_request(run_id):
    pages = tag.api(f'actions/runs/{run_id}/artifacts?per_page=100', paginate=True)
    artifacts = [a for p in pages for a in p['artifacts'] if a['name'] == 'tag-release-request']
    if len(artifacts) != 1 or artifacts[0]['expired']:
        raise ValueError('Original request missing/expired; no safe retry')
    archive = tag.api(f'actions/artifacts/{artifacts[0]["id"]}/zip', binary=True)
    with zipfile.ZipFile(io.BytesIO(archive)) as zipped:
        if (zipped.namelist() != ['tag-release-request.json'] or
                zipped.getinfo('tag-release-request.json').file_size > 16384):
            raise ValueError('Unexpected original request archive')
        return json.loads(zipped.read('tag-release-request.json'))


def verify_prebuild_retry(env):
    """Audit every earlier attempt, not merely the most recent failure."""
    attempt = int(env['GITHUB_RUN_ATTEMPT'])
    if attempt == 1:
        return
    original = artifact_request(int(env['GITHUB_RUN_ID']))
    if (original.get('source_mode') != MODE or original.get('retry_policy') != RETRY_POLICY or
            original.get('tag_source_sha') != env['GITHUB_SHA'] or
            original.get('tag_run_id') != int(env['GITHUB_RUN_ID']) or
            original.get('tag') != env['GITHUB_REF'].removeprefix('refs/tags/')):
        raise ValueError('Re-run requires the original direct-tag retry policy; legacy runs cannot resume')
    seen = set()
    allowed = {'tag_request', 'prepare', 'build', 'publish', 'manual_scope', 'recover_code2'}
    for prior in range(1, attempt):
        run = tag.api(f'actions/runs/{env["GITHUB_RUN_ID"]}/attempts/{prior}')
        if (run['id'] != int(env['GITHUB_RUN_ID']) or run['workflow_id'] != tag.WORKFLOW_ID or
                run['head_sha'] != env['GITHUB_SHA'] or run['event'] != 'push' or
                run['head_branch'] != original['tag'] or run['run_attempt'] != prior or
                run['run_number'] != int(env['GITHUB_RUN_NUMBER']) or run['status'] != 'completed' or
                run['repository']['full_name'] != tag.REPOSITORY):
            raise ValueError('Previous attempt identity is uncertain; no retry')
        pages = tag.api(f'actions/runs/{env["GITHUB_RUN_ID"]}/attempts/{prior}/jobs?per_page=100', paginate=True)
        jobs = [job for p in pages for job in p['jobs']]
        names = [job['name'] for job in jobs]
        if not jobs or len(names) != len(set(names)) or set(names) - allowed:
            raise ValueError('Unknown or incomplete earlier job history; no retry')
        for job in jobs:
            if job.get('status') != 'completed':
                raise ValueError('Previous jobs are still running; no retry')
            if job['name'] in {'build', 'publish', 'recover_code2', 'manual_scope'}:
                if job.get('conclusion') != 'skipped' or job.get('steps') != []:
                    raise ValueError('Build/publish may already have started; no versionCode reuse')
            seen.add(job['name'])
        if prior == 1 and set(names) != allowed:
            raise ValueError('Original job history is incomplete; no retry')
    if not {'build', 'publish'} <= seen:
        raise ValueError('Missing proof that build and publish were skipped')


def request_for_tag(event, env):
    if (env.get('GITHUB_REPOSITORY') != tag.REPOSITORY or event['repository']['full_name'] != tag.REPOSITORY or
            env.get('GITHUB_EVENT_NAME') != 'push' or not event.get('created') or event.get('forced') or
            event.get('deleted') or event.get('before') != '0' * 40 or env.get('GITHUB_REF_PROTECTED') != 'true'):
        raise ValueError('Only a new protected version tag in the original repository may deploy')
    if not re.fullmatch(r'[1-9][0-9]*', env.get('GITHUB_RUN_ATTEMPT', '')):
        raise ValueError('Invalid run attempt')
    name = env['GITHUB_REF'].removeprefix('refs/tags/')
    tag.version_from_tag(name)
    if event['ref'] != 'refs/tags/' + name:
        raise ValueError('Tag event ref mismatch')
    obj, source = tag.tag_identity(name)
    if env['GITHUB_SHA'] != source or event['after'] not in (obj, source):
        raise ValueError('Tag event source mismatch')
    pages = tag.api(f'actions/workflows/{tag.WORKFLOW_ID}/runs?per_page=100', paginate=True)
    runs = [run for p in pages for run in p['workflow_runs']]
    same = [run for run in runs if run['event'] == 'push' and run['head_branch'] == name]
    if len(same) != 1:
        raise ValueError('Tag moved/recreated or original push is ambiguous')
    run = same[0]
    if (run['id'] != int(env['GITHUB_RUN_ID']) or run['workflow_id'] != tag.WORKFLOW_ID or
            run['head_sha'] != source or run['run_attempt'] != int(env['GITHUB_RUN_ATTEMPT']) or
            run['run_number'] != int(env['GITHUB_RUN_NUMBER']) or
            run['repository']['full_name'] != tag.REPOSITORY):
        raise ValueError('Original workflow/run/counter/attempt mismatch')
    if max(r['run_number'] for r in runs) != run['run_number']:
        raise ValueError('Newer release workflow exists; stop stale release')
    verify_prebuild_retry(env)
    base = tag.api('git/ref/heads/main')['object']['sha']
    tag.preserve_release_code(base, source)
    request = dict(schema=2, source_mode=MODE, retry_policy=RETRY_POLICY,
                   tag=name, version_name=tag.version_from_tag(name), tag_object_sha=obj,
                   tag_source_sha=source, main_base_sha=base, tag_run_id=run['id'],
                   workflow_id=tag.WORKFLOW_ID, protected_tag=True)
    if int(env['GITHUB_RUN_ATTEMPT']) > 1 and artifact_request(run['id']) != request:
        raise ValueError('Original request or trusted main changed; no retry')
    return request


def manual_build_request(event, env):
    inputs = event.get('inputs', {})
    def enabled(name):
        return inputs.get(name) is True or inputs.get(name) == 'true'
    if (env.get('GITHUB_REPOSITORY') != tag.REPOSITORY or event['repository']['full_name'] != tag.REPOSITORY or
            env.get('GITHUB_EVENT_NAME') != 'workflow_dispatch' or env.get('GITHUB_REF') != 'refs/heads/dev' or
            not enabled('build_only') or enabled('publish_to_internal') or enabled('recover_original_code2')):
        raise ValueError('Build-only requires manual dispatch on dev with no publish/recovery permission')
    if env.get('GITHUB_RUN_ATTEMPT') != '1':
        raise ValueError('Manual build-only retries are disabled; dispatch a new build with a new code')
    version = tag.version_from_tag('v' + inputs.get('release_version', ''))
    source = env['GITHUB_SHA']
    if not re.fullmatch(r'[0-9a-f]{40}', source) or tag.api('git/ref/heads/dev')['object']['sha'] != source:
        raise ValueError('Manual build must pin the latest dev SHA; newer work is never mixed in')
    run = tag.api(f'actions/runs/{env["GITHUB_RUN_ID"]}')
    if (run['id'] != int(env['GITHUB_RUN_ID']) or run['workflow_id'] != tag.WORKFLOW_ID or
            run['event'] != 'workflow_dispatch' or run['head_branch'] != 'dev' or
            run['head_sha'] != source or run['run_attempt'] != 1 or
            run['run_number'] != int(env['GITHUB_RUN_NUMBER']) or
            run['repository']['full_name'] != tag.REPOSITORY):
        raise ValueError('Manual build run/workflow/source identity mismatch')
    pages = tag.api(f'actions/workflows/{tag.WORKFLOW_ID}/runs?per_page=100', paginate=True)
    runs = [row for page in pages for row in page['workflow_runs']]
    if (not any(row['id'] == run['id'] for row in runs) or
            max(row['run_number'] for row in runs) != run['run_number']):
        raise ValueError('Newer release/build run exists; no stale build')
    base = tag.api('git/ref/heads/main')['object']['sha']
    tag.preserve_release_code(base, source)
    return dict(schema=2, source_mode='manual-build-only-v1', version_name=version,
                source_commit=source, source_branch='dev', main_base_sha=base,
                build_run_id=run['id'], workflow_id=tag.WORKFLOW_ID)


def required_ci(source):
    # Respect both current rulesets and classic branch protection. Never change
    # protection or approve workflows. Inline offline release tests run as a
    # prerequisite job on this exact tag, independently of PR-created CI.
    pages = tag.api('rules/branches/dev?per_page=100', paginate=True)
    rules = [rule for page in pages for rule in page]
    if any(rule['type'] == 'workflows' for rule in rules):
        raise ValueError('Required workflow policy needs an exact-source CI verifier; no build/publish')
    required = [(row['context'], row.get('integration_id')) for rule in rules
                if rule['type'] == 'required_status_checks'
                for row in rule['parameters']['required_status_checks']]
    # The branch summary is readable with contents:read. The separate
    # /protection endpoint requires administration:read, which we do not add.
    branch = tag.api('branches/dev')
    if branch.get('protected') is not True:
        raise ValueError('dev must retain its branch protection')
    checks = branch.get('protection', {}).get('required_status_checks')
    if not isinstance(checks, dict):
        raise ValueError('Cannot read required CI configuration')
    if checks:
        if checks.get('checks'):
            required += [(row['context'], row.get('app_id')) for row in checks['checks']]
        elif checks.get('contexts'):
            # Classic summaries may omit checks[].app_id. A context alone
            # cannot prove that an untrusted producer is permitted. Do not
            # request new administration credentials or weaken the binding.
            raise ValueError('Classic required CI App bindings unavailable; no build/publish')
    if not required:
        return
    pages = tag.api(f'commits/{source}/check-runs?per_page=100', paginate=True)
    checks = [row for p in pages for row in p['check_runs']]
    pages = tag.api(f'commits/{source}/statuses?per_page=100', paginate=True)
    statuses = [row for p in pages for row in p]
    for context, app_id in required:
        matching = [row for row in checks if row['name'] == context and row['head_sha'] == source and
                    (app_id in (None, -1) or row['app']['id'] == app_id)]
        status = [row for row in statuses if row['context'] == context] if app_id in (None, -1) else []
        # A fresh pending/failed check must not fall back to an earlier success.
        latest = max(matching, key=lambda row: row['id']) if matching else None
        latest_status = max(status, key=lambda row: row['id']) if status else None
        ready = (latest and latest['status'] == 'completed' and latest['conclusion'] == 'success')
        if latest_status and latest_status['state'] != 'success':
            ready = False
        elif not latest and latest_status:
            ready = latest_status['state'] == 'success'
        if not ready:
            raise ValueError(f'Required CI incomplete on exact tag SHA: {context}; no build/publish. '
                             'After CI succeeds, use Re-run all jobs only if build/publish were skipped.')


def prepare_release(request, event, env):
    if request.get('source_mode') == 'manual-build-only-v1':
        if request != manual_build_request(event, env):
            raise ValueError('Manual build request changed')
        required_ci(request['source_commit'])
        return request
    if request != request_for_tag(event, env):
        raise ValueError('Original tag request changed')
    required_ci(request['tag_source_sha'])
    return dict(request, source_commit=request['tag_source_sha'])


def verify_release(release, env, event):
    if release.get('source_mode') == 'manual-build-only-v1':
        expected = manual_build_request(event, env)
        if (release != expected or env.get('BUILD_SHA') != expected['source_commit'] or
                tag.git('rev-parse', 'HEAD') != expected['source_commit']):
            raise ValueError('Manual build checkout/source provenance mismatch')
        required_ci(expected['source_commit'])
        return release
    expected = request_for_tag(event, env)
    if (release != dict(expected, source_commit=expected['tag_source_sha']) or
            env.get('BUILD_SHA') != expected['tag_source_sha'] or
            tag.git('rev-parse', 'HEAD') != expected['tag_source_sha']):
        raise ValueError('Build must use the exact protected dev tag SHA and original request')
    required_ci(expected['tag_source_sha'])
    return release


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['request', 'prepare', 'verify'])
    parser.add_argument('--request')
    args = parser.parse_args()
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    if args.mode == 'request':
        result = (manual_build_request(event, os.environ) if os.environ['GITHUB_EVENT_NAME'] == 'workflow_dispatch'
                  else request_for_tag(event, os.environ))
    else:
        request = json.loads(Path(args.request).read_text())
        result = prepare_release(request, event, os.environ) if args.mode == 'prepare' else verify_release(request, os.environ, event)
    Path('build').mkdir(exist_ok=True)
    filename = 'tag-release-request.json' if args.mode == 'request' else 'tag-release.json'
    Path('build', filename).write_text(json.dumps(result, indent=2) + '\n')
    source = result.get('tag_source_sha', result.get('source_commit'))
    label = result.get('tag', 'manual build-only ' + result['version_name'])
    with open(os.environ['GITHUB_OUTPUT'], 'a') as out:
        out.write(f'build_sha={source}\nversion_name={result["version_name"]}\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as out:
        out.write(f'### {label}\n\nExact build source: `{source}`\n\n'
                  'No deployment PR, Codex review or main merge. Required CI and artifact/Play safety checks remain.\n')


if __name__ == '__main__':
    main()
