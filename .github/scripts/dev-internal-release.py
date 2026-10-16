#!/usr/bin/env python3
"""One pinned dev device-validation release; no main merge or tag creation."""
import hashlib
import json
import os
from pathlib import Path
import sys
from urllib.parse import quote

PACKAGE = 'com.teamdoodle.doodledefense'
REF = 'refs/heads/build/internal-dev-f597dd18-1.0.3'
SOURCE = 'f597dd187a8f55708489a7451e964a1ce984e6fc'
APPROVED_CODE = 3
APPROVED_VERSION = '1.0.3'
VERIFIED_BUILD_COMMIT = '24c98d88fcef1fda5a17164ea25b02f1a2e8339b'
VERIFIED_AAB_SHA256 = 'f60d8546a3a29ce061e0ceec84d686f2ecbbf22600b452d6aeb15981ebe0a5a5'


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def allocate(env):
    require(env.get('GITHUB_REPOSITORY') == 'Sparta-fourTeam/client', 'Wrong repository')
    require(env.get('GITHUB_REF') == REF and env.get('GITHUB_EVENT_NAME') == 'workflow_dispatch', 'Wrong execution scope')
    require(env.get('GITHUB_RUN_ATTEMPT') == '1', 'Reruns forbidden: dispatch a new run after inspecting Play')
    require(env.get('GITHUB_WORKFLOW_REF', '').startswith('Sparta-fourTeam/client/.github/workflows/deploy-google-play.yml@'), 'Wrong workflow path')
    require(env.get('RELEASE_VERSION') == APPROVED_VERSION, 'Display version differs from approved 1.0.3')
    return APPROVED_CODE


def by_track(response):
    tracks = response.get('tracks', [])
    result = {item['track']: item for item in tracks}
    require(len(result) == len(tracks) and 'internal' in result, 'Missing/duplicate Play tracks')
    return result



def validate_internal_before_upload(track):
    releases = track.get('releases', [])
    completed = [item for item in releases if item.get('status') == 'completed'
                 and [str(value) for value in item.get('versionCodes', [])] == ['2']]
    empty = [item for item in releases if item.get('status') == 'draft'
             and not item.get('versionCodes') and not item.get('releaseNotes')
             and item.get('name') in (None, '', 'Untitled release', '제목 없는 버전', '제목없는 버전')
             and not item.get('userFraction') and not item.get('countryTargeting')
             and not item.get('inAppUpdatePriority')]
    require(len(completed) == 1 and len(empty) <= 1 and len(releases) == 1 + len(empty),
            'Internal track contains a different release or non-empty draft; inspect before proceeding')
    return bool(empty)


def publish(play, bundle, evidence, record):
    code = evidence['version_code']
    require(code == APPROVED_CODE and evidence['version_name'] == APPROVED_VERSION, 'Release differs from approved 1.0.3/code3')
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
    replacing_empty_draft = validate_internal_before_upload(original['internal'])
    codes = []
    for kind in ['bundles', 'apks']:
        codes.extend(int(item['versionCode']) for item in play.call('GET', path + '/' + kind).get(kind, []))
    track_codes = [int(value) for track in original.values() for item in track.get('releases', []) for value in item.get('versionCodes', [])]
    require(code not in codes and code not in track_codes, 'Code3 already used; stop without increment/retry')
    require(all(value < code for value in track_codes), 'A track contains a higher code; inspect before upload')
    record('play_preflight_verified', current_internal_code=2, candidate_code3_unused=True, replacing_empty_draft=replacing_empty_draft)
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
    record('commit_requested', track='internal', release_name=name)
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
        if not 200 <= response.status_code < 300:
            try:
                message = str(response.json().get('error', {}).get('message', ''))[:400]
            except ValueError:
                message = 'No structured Play error returned'
            raise RuntimeError(f'Play {method} failed HTTP {response.status_code}: {message}; no automatic retry or code change')
        return response.json()


def main():
    if sys.argv[1:] == ['allocate']:
        code = allocate(os.environ)
        with open(os.environ['GITHUB_OUTPUT'], 'a') as output:
            output.write(f"code={code}\nversion_name={os.environ['RELEASE_VERSION']}\n")
        print(f'Approved one-time 1.0.3 versionCode {code}; first attempt only')
        return
    require(sys.argv[1:] in (['publish'], ['publish-verified']), 'Unknown command')
    evidence = json.loads(Path('build/artifact-manifest.json').read_text())
    env = dict(os.environ, RELEASE_VERSION=evidence['version_name'])
    expected_source = VERIFIED_BUILD_COMMIT if sys.argv[1:] == ['publish-verified'] else env['GITHUB_SHA']
    require(evidence['source_commit'] == expected_source and allocate(env) == evidence['version_code'], 'Wrong build identity/code')
    if sys.argv[1:] == ['publish-verified']:
        require(evidence['artifact_sha256'] == VERIFIED_AAB_SHA256, 'Verified run38828424594 AAB hash mismatch')
    require(env.get('CONFIRM_INTERNAL') == 'true' and env.get('ACKNOWLEDGE_EDIT_INVALIDATION') == 'true', 'Internal publication/edit acknowledgment required')
    receipt = dict(source_commit=evidence['source_commit'], source_dev_commit=SOURCE, version_name=evidence['version_name'], version_code=evidence['version_code'], track='internal', uploader_commit=env['GITHUB_SHA'], events=[])
    def record(event, **values):
        receipt['events'].append(dict(event=event, **values))
        Path('build/internal-upload-receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
        print(event)
    bundle = Path('build/Android') / evidence['artifact']
    publish(PlayApi(), bundle, evidence, record)


if __name__ == '__main__':
    main()
