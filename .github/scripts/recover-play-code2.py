"""One fixed internal-release recovery; no build, version increment, or automatic retry."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
from urllib.parse import quote
import zipfile

REPOSITORY = 'Sparta-fourTeam/client'
SOURCE_RUN = 38347195550
ARTIFACT_ID = 11789029713
SOURCE_SHA = '10090077763379220123eabbabaedf7ae2ea3ed6'
ARCHIVE_SHA256 = 'aebb2295dacac7e3d6af97ccc3b71f52d5e1b93d1488112f09e93202cad8815f'
AAB_SHA256 = '3c6a2d6dbcc86f7efbde3474b88696264b35e26c7c91a9e76f842a5239e3f23a'
PACKAGE = 'com.teamdoodle.doodledefense'
VERSION = '1.0.2'
CODE = 2


class StopRecovery(Exception):
    pass


def require(condition, message):
    if not condition:
        raise StopRecovery(message)


def digest(path):
    with Path(path).open('rb') as source:
        return hashlib.file_digest(source, 'sha256').hexdigest()


def verify_artifact(archive, metadata, destination):
    require(metadata.get('id') == ARTIFACT_ID and metadata.get('expired') is False,
            'Wrong or expired source artifact')
    require(metadata.get('digest') == 'sha256:' + ARCHIVE_SHA256, 'Artifact metadata digest mismatch')
    run = metadata.get('workflow_run', {})
    require(run.get('id') == SOURCE_RUN and run.get('head_sha') == SOURCE_SHA
            and run.get('head_branch') == 'main', 'Artifact source run mismatch')
    require(digest(archive) == ARCHIVE_SHA256, 'Downloaded archive digest mismatch')
    with zipfile.ZipFile(archive) as files:
        require(sorted(files.namelist()) == sorted(['release-inputs.json', 'artifact-manifest.json',
                                                  'Android/Android.aab']), 'Unexpected archive contents')
        evidence = json.loads(files.read('artifact-manifest.json'))
        inputs = json.loads(files.read('release-inputs.json'))
        actual = evidence.get('actual', {})
        for record in [evidence, inputs]:
            require(record.get('source_commit') == SOURCE_SHA and record.get('package') == PACKAGE
                    and record.get('version_name') == VERSION and record.get('version_code') == CODE,
                    'Original artifact release identity mismatch')
        require(actual == {'package': PACKAGE, 'version_name': VERSION, 'version_code': CODE,
                           'target_api': 36}, 'Verified AAB identity mismatch')
        require(evidence.get('signature_verified') is True
                and evidence.get('architectures') == ['arm64-v8a']
                and evidence.get('artifact_sha256') == AAB_SHA256, 'AAB verification evidence mismatch')
        name = f"{actual['version_name']}({actual['version_code']})"
        require(len(name) <= 50, 'Play release name exceeds 50 characters')
        destination.mkdir(parents=True, exist_ok=True)
        bundle = destination / 'original.aab'
        with files.open('Android/Android.aab') as source, bundle.open('wb') as target:
            shutil.copyfileobj(source, target)
    require(digest(bundle) == AAB_SHA256, 'Original AAB file hash mismatch')
    return bundle, name


def check_execution(env):
    require(env.get('GITHUB_REPOSITORY') == REPOSITORY and env.get('GITHUB_REF') == 'refs/heads/main',
            'Recovery must run in the approved repository on main')
    require(env.get('GITHUB_RUN_ATTEMPT') == '1', 'Do not rerun an uncertain recovery attempt')
    require(env.get('CONFIRM_INTERNAL_CODE2') == 'true', 'Internal code2 recovery approval is required')
    require(env.get('ACKNOWLEDGE_EDIT_INVALIDATION') == 'true',
            'Creating/committing this edit can invalidate other pending edits; acknowledgment required')


def tracks_by_name(response):
    tracks = response.get('tracks', [])
    result = {track['track']: track for track in tracks}
    require(len(result) == len(tracks), 'Duplicate track in Play response')
    require('internal' in result, 'Internal track missing')
    return result


def validate_internal(track):
    releases = track.get('releases', [])
    require(len(releases) == 1 and releases[0].get('status') == 'completed'
            and releases[0].get('versionCodes') in [['1'], ['2']],
            'Unexpected internal draft/release; stop without replacing it')
    return releases[0]


def verify_play_bundle(bundle):
    require(bundle.get('versionCode') == CODE and bundle.get('sha256', '').lower() == AAB_SHA256,
            'Play code2 bundle hash or versionCode mismatch')


def recover(play, bundle, name, record):
    require(digest(bundle) == AAB_SHA256 and name == f'{VERSION}({CODE})' and len(name) <= 50,
            'Recovery inputs differ from original AAB')
    record('edit_creation_requested')
    edit = play.call('POST', '', body={})
    edit_id = edit.get('id')
    require(isinstance(edit_id, str) and bool(edit_id), 'No valid edit ID returned; do not retry')
    record('edit_created', edit_id=edit_id, expiry=edit.get('expiryTimeSeconds'))
    path = '/' + quote(edit_id, safe='')
    original_tracks = tracks_by_name(play.call('GET', path + '/tracks'))
    for track_name, track in original_tracks.items():
        if track_name != 'internal':
            require(all(release.get('status') == 'completed'
                        and '2' not in release.get('versionCodes', [])
                        for release in track.get('releases', [])),
                    'Unexpected draft/staged release or code2 on another track; stop')
    current = validate_internal(original_tracks['internal'])
    bundles = play.call('GET', path + '/bundles').get('bundles', [])
    existing = [item for item in bundles if item.get('versionCode') == CODE]
    require(len(existing) <= 1, 'Duplicate code2 bundles returned')
    if existing:
        verify_play_bundle(existing[0])
        record('existing_bundle_reused', edit_id=edit_id)
    else:
        require(current.get('versionCodes') != ['2'], 'Active code2 bundle missing; stop')
        record('upload_requested', edit_id=edit_id)
        # Exactly one upload. A used-code error or ambiguous response aborts, with no retry.
        uploaded = play.call('POST', path + '/bundles', bundle=bundle)
        verify_play_bundle(uploaded)
        record('original_bundle_uploaded', edit_id=edit_id)
    if current.get('versionCodes') == ['2'] and current.get('name') == name:
        record('already_published', edit_id=edit_id)
        return
    body = {'track': 'internal', 'releases': [{'name': name, 'versionCodes': ['2'],
                                               'status': 'completed'}]}
    play.call('PUT', path + '/tracks/internal', body=body)
    play.call('POST', path + ':validate')
    prepared = tracks_by_name(play.call('GET', path + '/tracks'))
    require({k: v for k, v in prepared.items() if k != 'internal'}
            == {k: v for k, v in original_tracks.items() if k != 'internal'},
            'Unrelated track changed; stop before commit')
    release = validate_internal(prepared['internal'])
    require(release.get('versionCodes') == ['2'] and release.get('name') == name,
            'Prepared internal release mismatch')
    record('commit_requested', edit_id=edit_id)
    committed = play.call('POST', path + ':commit')
    require(committed.get('id') == edit_id, 'Uncertain commit result; inspect state before any new attempt')
    record('committed', edit_id=edit_id)


class PlayApi:
    def __init__(self):
        from google.auth.transport.requests import AuthorizedSession, Request
        from google.oauth2 import service_account
        # Only the runner consumes the existing Actions secret; never write it or log it.
        info = json.loads(os.environ.pop('GOOGLE_PLAY_SERVICE_ACCOUNT_JSON'))
        credentials = service_account.Credentials.from_service_account_info(
            info, scopes=['https://www.googleapis.com/auth/androidpublisher'])
        credentials.refresh(Request())
        self.session = AuthorizedSession(credentials, max_refresh_attempts=0)

    def call(self, method, suffix, body=None, bundle=None):
        base = 'https://androidpublisher.googleapis.com/'
        route = 'androidpublisher/v3/applications/' + PACKAGE + '/edits' + suffix
        if bundle:
            with bundle.open('rb') as source:
                response = self.session.request(method, base + 'upload/' + route,
                    params={'uploadType': 'media'}, data=source,
                    headers={'Content-Type': 'application/octet-stream'}, timeout=(10, 180),
                    allow_redirects=False)
        else:
            response = self.session.request(method, base + route, json=body,
                                            timeout=(10, 120), allow_redirects=False)
        require(200 <= response.status_code < 300,
                f'Play {method} request failed (HTTP {response.status_code}); stop without retry or version increase')
        return response.json()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=['verify', 'publish'])
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--metadata', type=Path, required=True)
    parser.add_argument('--destination', type=Path, required=True)
    args = parser.parse_args()
    bundle, name = verify_artifact(args.archive, json.loads(args.metadata.read_text()), args.destination)
    print(f'Original artifact verified: {VERSION}({CODE}), AAB SHA256 {AAB_SHA256}')
    if args.mode == 'verify':
        return
    check_execution(os.environ)
    receipt = args.destination / 'recovery-receipt.json'
    state = dict(source_run=SOURCE_RUN, artifact_id=ARTIFACT_ID, source_sha=SOURCE_SHA,
                 aab_sha256=AAB_SHA256, release_name=name, events=[])
    def record(phase, **details):
        state.update(phase=phase, **details)
        state['events'].append(dict(phase=phase, **details))
        receipt.write_text(json.dumps(state, indent=2) + '\n')
        print(json.dumps(dict(phase=phase, **details)))
    recover(PlayApi(), bundle, name, record)


if __name__ == '__main__':
    try:
        main()
    except StopRecovery as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
    except Exception:
        # Raw auth/HTTP exceptions may contain sensitive values. Preserve the receipt, never print them.
        print('Recovery stopped after an unexpected/uncertain result; inspect receipt and Play state before retry',
              file=sys.stderr)
        sys.exit(1)
