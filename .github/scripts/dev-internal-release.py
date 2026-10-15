#!/usr/bin/env python3
"""One pinned dev device-validation release; no main merge or tag creation."""
import hashlib
import json
import os
from pathlib import Path
import re
import sys
from urllib.parse import quote

PACKAGE = 'com.teamdoodle.doodledefense'
REF = 'refs/heads/build/internal-dev-f597dd18-1.0.3'
SOURCE = 'f597dd187a8f55708489a7451e964a1ce984e6fc'
FLOOR = 214011987  # All-upload Play Console observation, including inactive uploads.


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def allocate(env):
    require(env.get('GITHUB_REPOSITORY') == 'Sparta-fourTeam/client', 'Wrong repository')
    require(env.get('GITHUB_REF') == REF and env.get('GITHUB_EVENT_NAME') == 'workflow_dispatch', 'Wrong execution scope')
    require(env.get('GITHUB_RUN_ATTEMPT') == '1', 'Reruns forbidden: dispatch a new run after inspecting Play')
    require(env.get('GITHUB_WORKFLOW_REF', '').startswith('Sparta-fourTeam/client/.github/workflows/deploy-google-play.yml@'), 'Workflow counter namespace changed')
    number = env.get('GITHUB_RUN_NUMBER', '')
    require(re.fullmatch(r'[1-9][0-9]*', number), 'Invalid workflow counter')
    code = 300000000 + int(number)
    require(FLOOR < code <= 2100000000, 'Version code outside approved namespace or Play limit')
    require(re.fullmatch(r'\d+(?:\.\d+){1,2}', env.get('RELEASE_VERSION', '')), 'Invalid display version')
    return code


def by_track(response):
    tracks = response.get('tracks', [])
    result = {item['track']: item for item in tracks}
    require(len(result) == len(tracks) and 'internal' in result, 'Missing/duplicate Play tracks')
    return result


def publish(play, bundle, evidence, record):
    code = evidence['version_code']
    digest = hashlib.sha256(bundle.read_bytes()).hexdigest()
    require(digest == evidence['artifact_sha256'], 'AAB changed after verification')
    require(evidence['actual'] == {k: evidence[k] for k in ['package', 'version_name', 'version_code', 'target_api']}, 'AAB manifest mismatch')
    require(evidence['package'] == PACKAGE and evidence['source_dev_commit'] == SOURCE and evidence['signature_verified'], 'Wrong source/package/signature')
    record('edit_creation_requested')
    edit = play.call('POST', '', body={})
    edit_id = edit.get('id')
    require(isinstance(edit_id, str) and bool(edit_id), 'Uncertain edit creation; inspect Play before retry')
    path = '/' + quote(edit_id, safe='')
    record('edit_created', edit_id=edit_id)
    original = by_track(play.call('GET', path + '/tracks'))
    releases = original['internal'].get('releases', [])
    require(len(releases) == 1 and releases[0].get('status') == 'completed' and releases[0].get('versionCodes') == ['2'], 'Internal track changed from approved code2; inspect before proceeding')
    codes = [FLOOR]
    for kind in ['bundles', 'apks']:
        codes.extend(int(item['versionCode']) for item in play.call('GET', path + '/' + kind).get(kind, []))
    codes.extend(int(value) for track in original.values() for release in track.get('releases', []) for value in release.get('versionCodes', []))
    require(max(codes) < code <= 2100000000, 'Play already contains this or a higher code; stop without increment/retry')
    record('play_preflight_verified', maximum_observed_code=max(codes), original_tracks=original)
    record('upload_requested')
    uploaded = play.call('POST', path + '/bundles', bundle=bundle)
    require(uploaded.get('versionCode') == code and uploaded.get('sha256', '').lower() == digest, 'Uploaded bundle identity mismatch')
    record('bundle_uploaded', version_code=code, artifact_sha256=digest)
    name = f"{evidence['version_name']}({code})"
    require(len(name) <= 50, 'Release name too long')
    desired = {'track': 'internal', 'releases': [{'name': name, 'versionCodes': [str(code)], 'status': 'completed'}]}
    play.call('PUT', path + '/tracks/internal', body=desired)
    play.call('POST', path + ':validate')
    prepared = by_track(play.call('GET', path + '/tracks'))
    require({k: v for k, v in original.items() if k != 'internal'} == {k: v for k, v in prepared.items() if k != 'internal'}, 'Unrelated track changed; no commit')
    actual_releases = prepared['internal'].get('releases', [])
    require(len(actual_releases) == 1 and all(actual_releases[0].get(k) == v for k, v in desired['releases'][0].items()), 'Internal prepared release mismatch')
    record('commit_requested', track='internal', prepared_internal=prepared['internal'])
    committed = play.call('POST', path + ':commit', params={'changesInReviewBehavior': 'ERROR_IF_IN_REVIEW'})
    require(committed.get('id') == edit_id, 'Commit uncertain; inspect Play before any new run')
    record('committed', edit_id=edit_id, track='internal', version_code=code)


class PlayApi:
    def __init__(self):
        from google.auth.transport.requests import AuthorizedSession, Request
        from google.oauth2 import service_account
        info = json.loads(os.environ.pop('GOOGLE_PLAY_SERVICE_ACCOUNT_JSON'))
        credentials = service_account.Credentials.from_service_account_info(info, scopes=['https://www.googleapis.com/auth/androidpublisher'])
        credentials.refresh(Request())
        self.session = AuthorizedSession(credentials, max_refresh_attempts=0)

    def call(self, method, suffix, body=None, bundle=None, params=None):
        base = 'https://androidpublisher.googleapis.com/'
        route = 'androidpublisher/v3/applications/' + PACKAGE + '/edits' + suffix
        if bundle:
            with bundle.open('rb') as source:
                response = self.session.request(method, base + 'upload/' + route, params={'uploadType': 'media'}, data=source,
                    headers={'Content-Type': 'application/octet-stream'}, timeout=(10, 180), allow_redirects=False)
        else:
            response = self.session.request(method, base + route, json=body, params=params, timeout=(10, 120), allow_redirects=False)
        require(200 <= response.status_code < 300, f'Play {method} failed HTTP {response.status_code}; no automatic retry')
        return response.json()


def main():
    if sys.argv[1:] == ['allocate']:
        code = allocate(os.environ)
        with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
            output.write(f"code={code}\nversion_name={os.environ['RELEASE_VERSION']}\n")
        print(f'Allocated versionCode {code}; first attempt only, shared workflow counter')
        return
    require(sys.argv[1:] == ['publish'], 'Unknown command')
    evidence = json.loads(Path('build/artifact-manifest.json').read_text())
    env = dict(os.environ, RELEASE_VERSION=evidence['version_name'])
    require(evidence['source_commit'] == env['GITHUB_SHA'] and allocate(env) == evidence['version_code'], 'Wrong build identity/code')
    require(env.get('CONFIRM_INTERNAL') == 'true' and env.get('ACKNOWLEDGE_EDIT_INVALIDATION') == 'true', 'Internal publication/edit acknowledgment required')
    receipt = dict(source_commit=evidence['source_commit'], source_dev_commit=SOURCE, version_name=evidence['version_name'], version_code=evidence['version_code'], track='internal', events=[])
    def record(event, **values):
        receipt['events'].append(dict(event=event, **values))
        Path('build/internal-upload-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
        print(event)
    bundle = Path('build/Android') / evidence['artifact']
    publish(PlayApi(), bundle, evidence, record)


if __name__ == '__main__':
    main()
