import copy
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('release', Path(__file__).with_name('publish-play-internal.py'))
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class InternalReleaseTests(unittest.TestCase):
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
