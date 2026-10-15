import copy
import hashlib
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('release', Path(__file__).with_name('dev-internal-release.py'))
release = importlib.util.module_from_spec(spec)
spec.loader.exec_module(release)


class InternalReleaseTests(unittest.TestCase):
    def test_shared_counter_floor_limit_and_rerun_guards(self):
        env = dict(GITHUB_REPOSITORY='Sparta-fourTeam/client', GITHUB_REF=release.REF,
                   GITHUB_EVENT_NAME='workflow_dispatch', GITHUB_RUN_ATTEMPT='1',
                   GITHUB_WORKFLOW_REF='Sparta-fourTeam/client/.github/workflows/deploy-google-play.yml@' + release.REF,
                   GITHUB_RUN_NUMBER='8', RELEASE_VERSION='1.0.3')
        self.assertEqual(release.allocate(env), 300000008)
        for key, value in [('GITHUB_RUN_ATTEMPT', '2'), ('GITHUB_REF', 'refs/heads/main'),
                           ('GITHUB_WORKFLOW_REF', 'Sparta-fourTeam/client/.github/workflows/new.yml@' + release.REF),
                           ('GITHUB_REPOSITORY', 'fork/client'), ('GITHUB_RUN_NUMBER', '1800000001')]:
            with self.subTest(key=key), self.assertRaises(RuntimeError):
                release.allocate(dict(env, **{key: value}))

    def exercise(self, higher=False, foreign_change=False, hash_mismatch=False, uncertain=False):
        with tempfile.TemporaryDirectory() as folder:
            bundle = Path(folder) / 'test.aab'
            bundle.write_bytes(b'verified fixture')
            evidence = dict(package=release.PACKAGE, version_name='1.0.3', version_code=300000008,
                            target_api=36, source_dev_commit=release.SOURCE, signature_verified=True,
                            artifact_sha256=hashlib.sha256(bundle.read_bytes()).hexdigest())
            evidence['actual'] = {k: evidence[k] for k in ['package', 'version_name', 'version_code', 'target_api']}
            tracks = {'internal': {'track': 'internal', 'releases': [{'status': 'completed', 'versionCodes': ['2']}]},
                      'production': {'track': 'production', 'releases': []}}
            calls = []
            class Play:
                def call(self, method, suffix, body=None, bundle=None, params=None):
                    calls.append((method, suffix, params))
                    if method == 'POST' and suffix == '':
                        return {'id': 'edit1'}
                    if suffix.endswith('/tracks'):
                        return {'tracks': list(copy.deepcopy(tracks).values())}
                    if method == 'GET' and suffix.endswith('/bundles'):
                        return {'bundles': [{'versionCode': 300000009 if higher else 214011987}]}
                    if suffix.endswith('/apks'):
                        return {'apks': []}
                    if method == 'POST' and suffix.endswith('/bundles'):
                        return {'versionCode': 300000008, 'sha256': 'wrong' if hash_mismatch else evidence['artifact_sha256']}
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
            if higher or foreign_change or hash_mismatch or uncertain:
                with self.assertRaises(RuntimeError):
                    release.publish(Play(), bundle, evidence, lambda event, **values: events.append(event))
            else:
                release.publish(Play(), bundle, evidence, lambda event, **values: events.append(event))
                self.assertEqual(events[-1], 'committed')
                self.assertIn(('POST', '/edit1:commit', {'changesInReviewBehavior': 'ERROR_IF_IN_REVIEW'}), calls)
            if higher:
                self.assertFalse(any(method == 'PUT' or suffix.endswith('/bundles') and method == 'POST' for method, suffix, _ in calls))
            if foreign_change or hash_mismatch:
                self.assertFalse(any(suffix.endswith(':commit') for _, suffix, _ in calls))
            if uncertain:
                self.assertNotIn('committed', events)
                self.assertEqual(sum(suffix.endswith(':commit') for _, suffix, _ in calls), 1)

    def test_internal_only_success_preserves_other_tracks(self):
        self.exercise()

    def test_higher_existing_code_stops_before_upload(self):
        self.exercise(higher=True)

    def test_unrelated_track_change_stops_before_commit(self):
        self.exercise(foreign_change=True)

    def test_server_bundle_hash_mismatch_stops_before_commit(self):
        self.exercise(hash_mismatch=True)

    def test_uncertain_commit_is_not_retried_or_claimed(self):
        self.exercise(uncertain=True)


if __name__ == '__main__':
    unittest.main()
