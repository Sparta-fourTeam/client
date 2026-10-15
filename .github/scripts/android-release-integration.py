"""Integrate one immutable tag through a reviewed PR; never bypass GitHub rules."""
import argparse
import importlib.util
import json
import os
from pathlib import Path
import re
import subprocess
import time

spec = importlib.util.spec_from_file_location('tag_gate', Path(__file__).with_name('android-tag-release.py'))
tag = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tag)
REPOSITORY = tag.REPOSITORY
# Public GitHub account identity checked during implementation, not a display name.
CODEX_ID = 199175422
CODEX_LOGIN = 'chatgpt-codex-connector[bot]'


def api(method, endpoint, payload=None, missing_ok=False):
    path = endpoint if endpoint == 'graphql' else f'repos/{REPOSITORY}/{endpoint}'
    args = ['gh', 'api', '--method', method, path]
    if payload is not None:
        args += ['--input', '-']
    result = subprocess.run(args, input=None if payload is None else json.dumps(payload),
                            text=True, capture_output=True)
    if result.returncode:
        if missing_ok and '(HTTP 404)' in result.stderr:
            return None
        # A denied/uncertain mutation is never retried. Do not log response bodies.
        raise ValueError(f'GitHub request failed: {method} {endpoint}; stop without bypass/retry')
    return json.loads(result.stdout) if result.stdout.strip() else None


def current_main():
    return api('GET', 'git/ref/heads/main')['object']['sha']


def check_request(request, env, event):
    tag.check_scope(event, env)
    if (env.get('GITHUB_EVENT_NAME') != 'push' or not event.get('created') or
            event.get('forced') or event.get('deleted') or event.get('before') != '0' * 40 or
            env.get('GITHUB_REF') != 'refs/tags/' + request['tag'] or
            request.get('tag_run_id') != int(env['GITHUB_RUN_ID']) or
            request.get('tag_source_sha') != env['GITHUB_SHA'] or
            request.get('version_name') != tag.version_from_tag(request['tag']) or
            request.get('workflow_id') != tag.WORKFLOW_ID or request.get('protected_tag') is not True):
        raise ValueError('Release request is not this original protected tag execution')
    obj, source = tag.tag_identity(request['tag'])
    if obj != request['tag_object_sha'] or source != request['tag_source_sha']:
        raise ValueError('Tag differs from original request')
    tag.tag_run(request['tag'], source, int(env['GITHUB_RUN_ID']))
    pages = tag.api(f'actions/workflows/{tag.WORKFLOW_ID}/runs?per_page=100', paginate=True)
    runs = [r for p in pages for r in p['workflow_runs']]
    if max(r['run_number'] for r in runs) != int(env['GITHUB_RUN_NUMBER']):
        raise ValueError('Newer release workflow exists; stop stale release')


def assert_pr(pr, request):
    if (pr['head']['repo']['full_name'] != REPOSITORY or pr['base']['repo']['full_name'] != REPOSITORY or
            pr['head']['ref'] != 'release/' + request['tag'] or pr['base']['ref'] != 'main' or
            pr['head']['sha'] != request['tag_source_sha'] or pr.get('draft')):
        raise ValueError('Release PR repository/ref/head SHA/draft mismatch')


def create_pr(request):
    branch = 'release/' + request['tag']
    existing = api('GET', 'git/ref/heads/' + branch, missing_ok=True)
    if existing is None:
        api('POST', 'git/refs', {'ref': 'refs/heads/' + branch, 'sha': request['tag_source_sha']})
    elif existing['object']['sha'] != request['tag_source_sha']:
        raise ValueError('Existing release branch changed; never force/update it')
    pulls = api('GET', f'pulls?state=all&base=main&head=Sparta-fourTeam:{branch}&per_page=100')
    if pulls:
        if len(pulls) != 1 or pulls[0]['state'] != 'open':
            raise ValueError('Release PR already consumed/closed or ambiguous')
        pr = pulls[0]
    else:
        pr = api('POST', 'pulls', {'title': '[release] ' + request['tag'], 'base': 'main', 'head': branch,
            'body': f'Tag-requested internal release.\n\nTag: `{request["tag"]}`\n'
                    f'Tag object: `{request["tag_object_sha"]}`\nSource: `{request["tag_source_sha"]}`\n'
                    f'Main base: `{request["main_base_sha"]}`\nRequest run: {request["tag_run_id"]}\n\n'
                    'Automatic merge requires latest-source Codex review, no unresolved review threads '
                    'and all GitHub protected-merge requirements. No self-approval or bypass.', 'draft': False})
    assert_pr(pr, request)
    api('POST', f'issues/{pr["number"]}/comments', {'body': '@codex review\n\n'
        f'Review this exact release source SHA: `{request["tag_source_sha"]}`. '
        'The release gate waits for a finalized review on that commit and no unresolved findings.'})
    return pr['number']


QUERY = '''query($number:Int!, $cursor:String) {
  repository(owner:"Sparta-fourTeam", name:"client") {
    pullRequest(number:$number) {
      headRefOid baseRefOid headRefName baseRefName isDraft state mergeable mergeStateStatus reviewDecision
      reviewThreads(first:100, after:$cursor) { nodes { isResolved } pageInfo { hasNextPage endCursor } }
    }
  }
}'''


def review_state(number):
    cursor, threads, initial = None, [], None
    while True:
        data = api('POST', 'graphql', {'query': QUERY, 'variables': {'number': number, 'cursor': cursor}})
        if data.get('errors'):
            raise ValueError('Review query failed; no merge')
        pr = data['data']['repository']['pullRequest']
        identity = {k: v for k, v in pr.items() if k != 'reviewThreads'}
        if initial is not None and initial != identity:
            raise ValueError('PR changed while reading review pages')
        initial = identity
        page = pr['reviewThreads']
        threads += page['nodes']
        if not page['pageInfo']['hasNextPage']:
            return dict(initial, threads=threads)
        cursor = page['pageInfo']['endCursor']
        if not cursor:
            raise ValueError('Incomplete review pagination')


def reviews(number):
    pages = tag.api(f'pulls/{number}/reviews?per_page=100', paginate=True)
    return [r for page in pages for r in page]


def review_gate(request, rows, state):
    if (state['headRefOid'] != request['tag_source_sha'] or state['baseRefName'] != 'main' or
            state['headRefName'] != 'release/' + request['tag']):
        raise ValueError('PR head moved after review request')
    if state['isDraft'] or state['reviewDecision'] in ('CHANGES_REQUESTED', 'REVIEW_REQUIRED'):
        return None
    if any(not thread['isResolved'] for thread in state['threads']):
        return None
    latest, blocking = {}, set()
    for row in sorted(rows, key=lambda r: (r.get('submitted_at') or '9999', r['id'])):
        actor = row['user']['id']
        latest[actor] = row
        if row['state'] == 'CHANGES_REQUESTED':
            blocking.add(actor)
        elif row['state'] in ('APPROVED', 'DISMISSED'):
            blocking.discard(actor)
    # A later COMMENTED review does not withdraw a change request.
    if blocking:
        return None
    review = latest.get(CODEX_ID)
    if (not review or review['user'].get('type') != 'Bot' or review['user'].get('login') != CODEX_LOGIN or
            review['commit_id'] != request['tag_source_sha'] or
            review['state'] not in ('APPROVED', 'COMMENTED') or not review.get('submitted_at')):
        return None
    # COMMENTED is a finalized review, not a substitute GitHub approval. Inline
    # findings must be resolved; top-level priority findings require a new review.
    if re.search(r'\[P[0-3]\]', review.get('body') or '', re.IGNORECASE):
        return None
    return review


def verify_remote_merge(request):
    pr = api('GET', f'pulls/{request["pr_number"]}')
    assert_pr(pr, request)
    merged = request['merge_commit_sha']
    if not pr['merged'] or pr['merge_commit_sha'] != merged or current_main() != merged:
        raise ValueError('Actual main/PR differs from recorded release merge')
    commit = api('GET', 'git/commits/' + merged)
    if ([p['sha'] for p in commit['parents']] != [request['main_base_sha'], request['tag_source_sha']] or
            commit['tree']['sha'] != request['integrated_tree']):
        raise ValueError('Merged parents/tree differ from exact release integration')
    rows = reviews(pr['number'])
    if not any(r['id'] == request['codex_review_id'] and r['user']['id'] == CODEX_ID and
               r['commit_id'] == request['tag_source_sha'] for r in rows):
        raise ValueError('Original Codex review evidence is missing')
    if not review_gate(request, rows, review_state(pr['number'])):
        raise ValueError('Latest-SHA Codex review or unresolved-review gate no longer passes')


def record_attempt(request, number, phase, **extra):
    Path('build').mkdir(exist_ok=True)
    Path('build/release-integration-attempt.json').write_text(json.dumps(
        dict(request, pr_number=number, phase=phase, **extra), indent=2) + '\n')


def integrate(request, env, event, timeout=1200, interval=20, clock=time.monotonic, sleep=time.sleep):
    # All checks precede the first repository mutation.
    check_request(request, env, event)
    if tag.prepare_request(event, env) != request or current_main() != request['main_base_sha']:
        raise ValueError('Main/request changed after read-only preflight')
    tag.preserve_release_code(request['main_base_sha'], request['tag_source_sha'])
    tree = tag.merge_tree(request['main_base_sha'], request['tag_source_sha'])
    version_path = Path(__file__).with_name('android-release-version.py')
    version_spec = importlib.util.spec_from_file_location('version_policy', version_path)
    version = importlib.util.module_from_spec(version_spec)
    version_spec.loader.exec_module(version)
    policy = json.loads(tag.git('show', request['main_base_sha'] + ':.github/android-release.json'))
    version.candidate_code(policy, request['version_name'], policy, int(env['GITHUB_RUN_NUMBER']))
    record_attempt(request, None, 'creating_pr')
    number = create_pr(request)
    record_attempt(request, number, 'awaiting_review')
    deadline = clock() + timeout
    while True:
        check_request(request, env, event)
        if current_main() != request['main_base_sha']:
            raise ValueError('Main advanced while awaiting review; no automatic rebase')
        pr = api('GET', f'pulls/{number}')
        assert_pr(pr, request)
        if pr['state'] != 'open' or pr['merged']:
            raise ValueError('PR was closed/merged outside this gated execution')
        state = review_state(number)
        if state['baseRefOid'] != request['main_base_sha'] or state['mergeable'] == 'CONFLICTING':
            raise ValueError('PR base changed or conflicts; no automatic resolution')
        completed = review_gate(request, reviews(number), state)
        if completed and state['mergeable'] == 'MERGEABLE' and state['mergeStateStatus'] in ('CLEAN', 'UNSTABLE'):
            # GitHub remains authoritative for required CI/reviews and all rules.
            # UNSTABLE may include this still-running, non-required tag workflow.
            # No --admin, review dismissal, self approval, or bypass mutation.
            check_request(request, env, event)
            if current_main() != request['main_base_sha']:
                raise ValueError('Main moved before protected merge request')
            record_attempt(request, number, 'merge_requested', codex_review_id=completed['id'], integrated_tree=tree)
            response = api('PUT', f'pulls/{number}/merge', {'sha': request['tag_source_sha'], 'merge_method': 'merge'})
            record_attempt(request, number, 'merge_returned', returned_sha=response.get('sha'), merged=response.get('merged'))
            if not response.get('merged') or not re.fullmatch(r'[0-9a-f]{40}', response.get('sha', '')):
                raise ValueError('Merge not confirmed; stop without retry or deployment')
            result = dict(request, pr_number=number, source_commit=response['sha'], merge_commit_sha=response['sha'],
                          integrated_tree=tree, codex_review_id=completed['id'], codex_commit_sha=completed['commit_id'])
            verify_remote_merge(result)
            record_attempt(request, number, 'verified', merged_sha=response['sha'], codex_review_id=completed['id'])
            return result
        if clock() >= deadline:
            raise ValueError('Timed out waiting for latest-SHA Codex review, resolved threads, or required GitHub checks')
        sleep(interval)


def verify_release(request, env, event):
    check_request(request, env, event)
    if env.get('BUILD_SHA') != request.get('merge_commit_sha') or tag.git('rev-parse', 'HEAD') != env['BUILD_SHA']:
        raise ValueError('Build checkout is not the exact recorded merge SHA')
    tag.preserve_release_code(request['main_base_sha'], request['tag_source_sha'])
    if (tag.merge_tree(request['main_base_sha'], request['tag_source_sha']) != request['integrated_tree'] or
            request['source_commit'] != env['BUILD_SHA'] or request['codex_commit_sha'] != request['tag_source_sha']):
        raise ValueError('Build provenance does not match reviewed tag integration')
    verify_remote_merge(request)
    return request


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['integrate', 'verify'])
    parser.add_argument('--request', required=True)
    args = parser.parse_args()
    request = json.loads(Path(args.request).read_text())
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    result = integrate(request, os.environ, event) if args.mode == 'integrate' else verify_release(request, os.environ, event)
    Path('build').mkdir(exist_ok=True)
    Path('build/tag-release.json').write_text(json.dumps(result, indent=2) + '\n')
    with open(os.environ['GITHUB_OUTPUT'], 'a') as out:
        out.write(f'merged_sha={result["merge_commit_sha"]}\nversion_name={result["version_name"]}\npr_number={result["pr_number"]}\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a') as out:
        out.write(f'### Integrated release {result["tag"]}\n\nTag source: `{result["tag_source_sha"]}`  \n'
                  f'Merged build SHA: `{result["merge_commit_sha"]}`  \nPR: #{result["pr_number"]}  \n'
                  f'Codex review: {result["codex_review_id"]} on `{result["codex_commit_sha"]}`\n')


if __name__ == '__main__':
    main()
