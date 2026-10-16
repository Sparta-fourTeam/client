"""Publish one verified tag AAB to internal; never retry or choose another code."""
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
from urllib.parse import quote

PACKAGE = 'com.teamdoodle.doodledefense'


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def tracks_by_name(response):
    tracks = response.get('tracks', [])
    result = {item['track']: item for item in tracks}
    require(len(result) == len(tracks) and 'internal' in result, 'Missing/duplicate Play tracks')
    return result


def validate_internal(track, code):
    releases = track.get('releases', [])
    completed = [r for r in releases if r.get('status') == 'completed' and r.get('versionCodes')
                 and all(0 < int(value) < code for value in r['versionCodes'])]
    empty = [r for r in releases if r.get('status') == 'draft'
             and not r.get('versionCodes') and not r.get('releaseNotes')
             and r.get('name') in (None, '', 'Untitled release', '제목 없는 버전', '제목없는 버전')
             and not r.get('userFraction') and not r.get('countryTargeting')
             and not r.get('inAppUpdatePriority')]
    require(len(completed) == 1 and len(empty) <= 1 and len(releases) == 1 + len(empty),
            'Unexpected internal release or non-empty draft; inspect Play before proceeding')
    return bool(empty)


def publish(play, bundle, evidence, record, authorize):
    code, version = evidence['version_code'], evidence['version_name']
    actual = evidence['actual']
    require(type(code) is int and 0 < code <= 2100000000, 'Invalid versionCode')
    require(actual['package'] == PACKAGE and actual['version_code'] == code
            and actual['version_name'] == version and actual['target_api'] >= 36
            and evidence['signature_verified'], 'AAB manifest/package/signature mismatch')
    digest = hashlib.sha256(bundle.read_bytes()).hexdigest()
    require(digest == evidence['artifact_sha256'], 'AAB changed after verification')
    name = f'{version}({code})'
    require(len(name) <= 50, 'Play release name exceeds 50 characters')
    authorize()
    record('edit_creation_requested')
    edit_id = play.call('POST', '', body={}).get('id')
    require(isinstance(edit_id, str) and bool(edit_id), 'Uncertain edit creation; inspect Play before retry')
    path = '/' + quote(edit_id, safe='')
    record('edit_created', edit_id=edit_id)
    original = tracks_by_name(play.call('GET', path + '/tracks'))
    empty = validate_internal(original['internal'], code)
    used = []
    for kind in ['bundles', 'apks']:
        used += [int(item['versionCode']) for item in play.call('GET', path + '/' + kind).get(kind, [])]
    track_codes = [int(value) for track in original.values() for r in track.get('releases', [])
                   for value in r.get('versionCodes', [])]
    require(code not in used and code not in track_codes, 'Candidate code already used; stop without increment/retry')
    # Accepted inactive drafts reserve their own code, not all smaller codes.
    require(all(value < code for value in track_codes), 'A track contains a higher code; inspect Play before upload')
    record('play_preflight_verified', candidate_code=code, replacing_empty_draft=empty)
    record('upload_requested')
    uploaded = play.call('POST', path + '/bundles', bundle=bundle)
    require(uploaded.get('versionCode') == code and uploaded.get('sha256', '').lower() == digest,
            'Uploaded bundle identity mismatch')
    record('bundle_uploaded', version_code=code, artifact_sha256=digest)
    desired = {'track': 'internal', 'releases': [{'name': name, 'versionCodes': [str(code)], 'status': 'completed'}]}
    play.call('PUT', path + '/tracks/internal', body=desired)
    play.call('POST', path + ':validate')
    prepared = tracks_by_name(play.call('GET', path + '/tracks'))
    require({k: v for k, v in original.items() if k != 'internal'} ==
            {k: v for k, v in prepared.items() if k != 'internal'}, 'Unrelated track changed; no commit')
    releases = prepared['internal'].get('releases', [])
    require(len(releases) == 1 and all(releases[0].get(k) == v for k, v in desired['releases'][0].items()),
            'Internal prepared release mismatch')
    # Recheck current main, original run, immutable tag, PR/review and CI after
    # uploading too. A change/uncertain result stops; there is no publish retry.
    authorize()
    record('commit_requested', track='internal', release_name=name)
    committed = play.call('POST', path + ':commit', params={'changesInReviewBehavior': 'ERROR_IF_IN_REVIEW'})
    require(committed.get('id') == edit_id, 'Commit uncertain; inspect Play before any new attempt')
    record('committed', edit_id=edit_id, track='internal', version_code=code)


def main():
    evidence = json.loads(Path('verified/artifact-manifest.json').read_text())
    release = json.loads(Path('build/tag-release.json').read_text())
    require(all(evidence.get(k) == v for k, v in release.items()), 'Artifact tag/PR/merge provenance mismatch')
    require(evidence['source_commit'] == os.environ['BUILD_SHA'] and
            evidence['version_code'] == int(os.environ['ANDROID_VERSION_CODE']) and
            evidence['version_name'] == os.environ['RELEASE_VERSION'], 'Artifact build identity mismatch')
    files = list(Path('verified/Android').glob('*.aab'))
    require(len(files) == 1, 'Exactly one verified AAB required')
    state = dict(source_commit=evidence['source_commit'], actions_run=int(os.environ['GITHUB_RUN_ID']),
                 version_name=evidence['version_name'], version_code=evidence['version_code'],
                 version_code_allocation=evidence['version_code_allocation'], track='internal', events=[])
    def record(event, **values):
        state['events'].append(dict(event=event, **values))
        Path('build/internal-upload-receipt.json').write_text(json.dumps(state, indent=2) + '\n')
        print(event)
    def authorize():
        subprocess.run([sys.executable, str(Path(__file__).with_name('android-release-version.py')),
                        '--automatic', '--release-request', 'release-context/tag-release.json',
                        '--expected-code', str(evidence['version_code'])], check=True)
    spec = importlib.util.spec_from_file_location('play_recovery', Path(__file__).with_name('recover-play-code2.py'))
    recovery = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(recovery)
    publish(recovery.PlayApi(), files[0], evidence, record, authorize)


if __name__ == '__main__':
    try:
        main()
    except RuntimeError as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
    except Exception:
        # Auth/HTTP exception bodies can contain private values; receipts contain
        # only artifact identity and phases, never raw Play responses or keys.
        print('Internal publish stopped after an unexpected/uncertain result; inspect receipt and Play before retry', file=sys.stderr)
        sys.exit(1)
