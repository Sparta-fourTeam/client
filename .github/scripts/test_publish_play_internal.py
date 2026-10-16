import copy
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

spec = importlib.util.spec_from_file_location('release', Path(__file__).with_name('publish-play-internal.py'))
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class InternalReleaseTests(unittest.TestCase):
    def test_manual_build_only_artifact_is_rejected_before_any_play_access(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'verified').mkdir()
            (root / 'release-context').mkdir()
            (root / 'verified/artifact-manifest.json').write_text('{}')
            (root / 'release-context/tag-release.json').write_text(json.dumps({'source_mode': 'manual-build-only-v1'}))
            before = Path.cwd()
            try:
                os.chdir(root)
                with patch.object(release.subprocess, 'run', side_effect=AssertionError('External access forbidden')):
                    with self.assertRaisesRegex(RuntimeError, 'Build-only artifacts cannot be published'):
                        release.main()
            finally:
                os.chdir(before)

    def test_initial_receipt_survives_first_release_authorization_failure(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            (root / 'verified/Android').mkdir(parents=True)
            (root / 'build').mkdir()
            (root / 'release-context').mkdir()
            bundle = root / 'verified/Android/test.aab'
            bundle.write_bytes(b'verified offline fixture')
            evidence = dict(source_commit='merge', version_code=4, version_name='1.0.4', pr_number=123,
                            artifact_sha256=hashlib.sha256(bundle.read_bytes()).hexdigest(),
                            version_code_allocation={'scheme': 'workflow-run-offset-v1', 'workflowId': 378016847, 'runNumber': 12})
            (root / 'verified/artifact-manifest.json').write_text(json.dumps(evidence))
            (root / 'release-context/tag-release.json').write_text(json.dumps({'source_commit': 'merge'}))
            env = dict(BUILD_SHA='merge', ANDROID_VERSION_CODE='4', RELEASE_VERSION='1.0.4', GITHUB_RUN_ID='42',
                       GITHUB_STEP_SUMMARY=str(root / 'summary'))
            previous = Path.cwd()
            try:
                os.chdir(root)
                with patch.dict(os.environ, env), patch.object(release.sys, 'argv', ['publish', '--prepare']):
                    release.main()
                with patch.dict(os.environ, env), patch.object(release.sys, 'argv', ['publish']), \
                        patch.object(release.subprocess, 'run', side_effect=subprocess.CalledProcessError(1, ['offline'])):
                    with self.assertRaises(subprocess.CalledProcessError):
                        release.main()
                receipt = json.loads((root / 'build/internal-upload-receipt.json').read_text())
                self.assertEqual(receipt['artifact_sha256'], evidence['artifact_sha256'])
                self.assertEqual([r['event'] for r in receipt['events']], ['prepared', 'release_preflight_requested', 'stopped'])
            finally:
                os.chdir(previous)

    def test_only_truly_empty_unnamed_draft_can_accompany_completed_release(self):
        completed = {'status': 'completed', 'versionCodes': ['3']}
        empty = {'status': 'draft'}
        self.assertTrue(release.validate_internal({'releases': [completed, empty]}, 4))
        for changed in [dict(empty, versionCodes=['3']), dict(empty, releaseNotes=[{'text': 'work in progress'}]),
                        dict(empty, name='Another planned release'), dict(empty, userFraction=0.1),
                        dict(empty, countryTargeting={'countries': ['KR']}), dict(empty, inAppUpdatePriority=5)]:
            with self.subTest(draft=changed), self.assertRaises(RuntimeError):
                release.validate_internal({'releases': [completed, changed]}, 4)

    def exercise(self, used=False, active_higher=False, foreign_change=False, hash_mismatch=False, uncertain=False, stale=False):
        with tempfile.TemporaryDirectory() as folder:
            bundle = Path(folder) / 'test.aab'
            bundle.write_bytes(b'verified fixture')
            evidence = dict(package=release.PACKAGE, version_name='1.0.4', version_code=4,
                            target_api=36, signature_verified=True,
                            artifact_sha256=hashlib.sha256(bundle.read_bytes()).hexdigest())
            evidence['actual'] = {k: evidence[k] for k in ['package', 'version_name', 'version_code', 'target_api']}
            tracks = {'internal': {'track': 'internal', 'releases': [{'status': 'completed', 'versionCodes': ['3']}, {'status': 'draft'}]},
                      'production': {'track': 'production', 'releases': []}}
            if active_higher:
                tracks['production']['releases'] = [{'status': 'completed', 'versionCodes': ['5']}]
            calls = []
            class Play:
                def call(self, method, suffix, body=None, bundle=None, params=None):
                    calls.append((method, suffix, params))
                    if method == 'POST' and suffix == '':
                        return {'id': 'edit1'}
                    if suffix.endswith('/tracks'):
                        return {'tracks': list(copy.deepcopy(tracks).values())}
                    if method == 'GET' and suffix.endswith('/bundles'):
                        return {'bundles': [{'versionCode': 4 if used else 214011987}]}
                    if suffix.endswith('/apks'):
                        return {'apks': []}
                    if method == 'POST' and suffix.endswith('/bundles'):
                        return {'versionCode': 4, 'sha256': 'wrong' if hash_mismatch else evidence['artifact_sha256']}
                    if method == 'PUT':
                        self.assert_internal(suffix)
                        tracks['internal'] = copy.deepcopy(body)
                        if foreign_change:
                            tracks['production']['releases'] = [{'versionCodes': ['9']}]
                    if suffix.endswith(':commit'):
                        return {'id': 'wrong' if uncertain else 'edit1'}
                    return {}

                def assert_internal(self, suffix):
                    if suffix != '/edit1/tracks/internal':
                        raise AssertionError('Unexpected write target')
            events = []
            authorizations = []
            def authorize():
                authorizations.append(True)
                if stale and len(authorizations) == 2:
                    raise RuntimeError('Main/review changed during upload')
            if used or active_higher or foreign_change or hash_mismatch or uncertain or stale:
                with self.assertRaises(RuntimeError):
                    release.publish(Play(), bundle, evidence, lambda event, **values: events.append(event), authorize)
            else:
                release.publish(Play(), bundle, evidence, lambda event, **values: events.append(event), authorize)
                self.assertEqual(events[-1], 'committed')
                self.assertEqual(len(authorizations), 2)
                self.assertIn(('POST', '/edit1:commit', {'changesInReviewBehavior': 'ERROR_IF_IN_REVIEW'}), calls)
            if used or active_higher:
                self.assertFalse(any(method == 'PUT' or suffix.endswith('/bundles') and method == 'POST' for method, suffix, _ in calls))
            if foreign_change or hash_mismatch or stale:
                self.assertFalse(any(suffix.endswith(':commit') for _, suffix, _ in calls))
            if uncertain:
                self.assertNotIn('committed', events)
                self.assertEqual(sum(suffix.endswith(':commit') for _, suffix, _ in calls), 1)

    def test_internal_only_success_preserves_other_tracks(self):
        self.exercise()

    def test_already_used_candidate_stops_before_upload(self):
        self.exercise(used=True)

    def test_higher_active_track_code_stops_before_upload(self):
        self.exercise(active_higher=True)

    def test_unrelated_track_change_stops_before_commit(self):
        self.exercise(foreign_change=True)

    def test_server_bundle_hash_mismatch_stops_before_commit(self):
        self.exercise(hash_mismatch=True)

    def test_stale_release_after_upload_cannot_commit(self):
        self.exercise(stale=True)

    def test_uncertain_commit_is_not_retried_or_claimed(self):
        self.exercise(uncertain=True)


if __name__ == '__main__':
    unittest.main()
