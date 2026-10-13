import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('recovery', Path(__file__).with_name('recover-play-code2.py'))
recovery = importlib.util.module_from_spec(spec)
spec.loader.exec_module(recovery)


class FakePlay:
    def __init__(self, existing=False, internal_code='1', status='completed', failure=None):
        self.calls = []
        self.failure = failure
        self.bundles = [{'versionCode': 2, 'sha256': 'fixture-hash'}] if existing else []
        self.tracks = {'tracks': [
            {'track': 'internal', 'releases': [{'versionCodes': [internal_code], 'status': status,
                                               'name': '1.0.2(2)' if internal_code == '2' else 'previous'}]},
            {'track': 'production', 'releases': [{'versionCodes': ['9'], 'status': 'completed'}]}]}

    def call(self, method, suffix, body=None, bundle=None):
        self.calls.append((method, suffix, body, bundle))
        if method == 'POST' and suffix == '':
            return {'id': 'edit-123', 'expiryTimeSeconds': '9999999999'}
        if suffix.endswith('/tracks'):
            return copy.deepcopy(self.tracks)
        if method == 'GET' and suffix.endswith('/bundles'):
            return {'bundles': copy.deepcopy(self.bundles)}
        if bundle is not None:
            if self.failure == 'used-code':
                raise recovery.StopRecovery('Version code already used; no retry')
            if self.failure == 'upload-timeout':
                raise TimeoutError('ambiguous upload')
            return {'versionCode': 2, 'sha256': 'fixture-hash'}
        if method == 'PUT':
            self.tracks['tracks'][0] = copy.deepcopy(body)
            return copy.deepcopy(body)
        if suffix.endswith(':validate'):
            if self.failure == 'unrelated-track':
                self.tracks['tracks'][1]['releases'] = []
            return {'id': 'edit-123'}
        if suffix.endswith(':commit'):
            if self.failure == 'commit-timeout':
                raise TimeoutError('ambiguous commit')
            return {'id': 'edit-123'}
        raise AssertionError((method, suffix))


class RecoveryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.bundle = self.root / 'test.aab'
        self.bundle.write_bytes(b'fixture bytes')
        self.records = []

    def run_recovery(self, play):
        with patch.object(recovery, 'AAB_SHA256', 'fixture-hash'), patch.object(recovery, 'digest', return_value='fixture-hash'):
            recovery.recover(play, self.bundle, '1.0.2(2)', lambda phase, **details: self.records.append((phase, details)))

    def test_existing_matching_bundle_is_reused_without_upload(self):
        play = FakePlay(existing=True)
        self.run_recovery(play)
        self.assertFalse(any(call[3] for call in play.calls))
        self.assertEqual(self.records[-1][0], 'committed')
        self.assertEqual([c[2] for c in play.calls if c[0] == 'PUT'],
                         [{'track': 'internal', 'releases': [{'name': '1.0.2(2)', 'versionCodes': ['2'], 'status': 'completed'}]}])

    def test_absent_bundle_uploads_original_path_exactly_once(self):
        play = FakePlay()
        self.run_recovery(play)
        self.assertEqual([call[3] for call in play.calls if call[3]], [self.bundle])
        self.assertEqual(self.records[-1][0], 'committed')

    def test_existing_hash_mismatch_stops_without_upload_update_or_commit(self):
        play = FakePlay(existing=True)
        play.bundles[0]['sha256'] = 'other-binary'
        with self.assertRaises(recovery.StopRecovery):
            self.run_recovery(play)
        self.assertFalse(any(c[3] or c[0] == 'PUT' or c[1].endswith(':commit') for c in play.calls))

    def test_used_code_or_uncertain_upload_aborts_without_retry(self):
        for failure in ['used-code', 'upload-timeout']:
            with self.subTest(failure=failure):
                play = FakePlay(failure=failure)
                with self.assertRaises((recovery.StopRecovery, TimeoutError)):
                    self.run_recovery(play)
                self.assertEqual(sum(c[3] is not None for c in play.calls), 1)
                self.assertFalse(any(c[0] == 'PUT' or c[1].endswith(':commit') for c in play.calls))

    def test_unrelated_internal_draft_or_version_stops_before_upload(self):
        for code, status in [('1', 'draft'), ('3', 'completed')]:
            play = FakePlay(internal_code=code, status=status)
            with self.assertRaises(recovery.StopRecovery):
                self.run_recovery(play)
            self.assertFalse(any(c[3] or c[0] == 'PUT' for c in play.calls))

    def test_unrelated_track_difference_blocks_commit(self):
        play = FakePlay(existing=True, failure='unrelated-track')
        with self.assertRaises(recovery.StopRecovery):
            self.run_recovery(play)
        self.assertFalse(any(c[1].endswith(':commit') for c in play.calls))

    def test_initial_other_track_draft_or_code2_stops_before_upload(self):
        for status, code in [('draft', '9'), ('inProgress', '9'), ('completed', '2')]:
            with self.subTest(status=status, code=code):
                play = FakePlay()
                play.tracks['tracks'][1]['releases'] = [{'status': status, 'versionCodes': [code]}]
                with self.assertRaises(recovery.StopRecovery):
                    self.run_recovery(play)
                self.assertFalse(any(c[3] or c[0] == 'PUT' or c[1].endswith(':commit') for c in play.calls))

    def test_ambiguous_commit_is_not_retried_and_receipt_records_intent(self):
        play = FakePlay(existing=True, failure='commit-timeout')
        with self.assertRaises(TimeoutError):
            self.run_recovery(play)
        self.assertEqual(sum(c[1].endswith(':commit') for c in play.calls), 1)
        self.assertEqual(self.records[-1][0], 'commit_requested')

    def test_already_published_matching_release_does_not_upload_or_commit(self):
        play = FakePlay(existing=True, internal_code='2')
        self.run_recovery(play)
        self.assertEqual(self.records[-1][0], 'already_published')
        self.assertFalse(any(c[3] or c[0] == 'PUT' or c[1].endswith(':commit') for c in play.calls))

    def test_gate_rejects_other_scope_missing_ack_and_rerun(self):
        env = {'GITHUB_REPOSITORY': recovery.REPOSITORY, 'GITHUB_REF': 'refs/heads/main',
               'GITHUB_RUN_ATTEMPT': '1', 'CONFIRM_INTERNAL_CODE2': 'true',
               'ACKNOWLEDGE_EDIT_INVALIDATION': 'true'}
        recovery.check_execution(env)
        for key, value in [('GITHUB_REPOSITORY', 'other/repo'), ('GITHUB_REF', 'refs/heads/dev'),
                           ('GITHUB_RUN_ATTEMPT', '2'), ('CONFIRM_INTERNAL_CODE2', 'false'),
                           ('ACKNOWLEDGE_EDIT_INVALIDATION', 'false')]:
            with self.subTest(key=key), self.assertRaises(recovery.StopRecovery):
                recovery.check_execution(dict(env, **{key: value}))

    def make_archive(self, version='1.0.2', payload=b'fixture bytes'):
        aab_hash = hashlib.sha256(b'fixture bytes').hexdigest()
        inputs = {'source_commit': recovery.SOURCE_SHA, 'package': recovery.PACKAGE,
                  'version_name': version, 'version_code': 2}
        evidence = dict(inputs, actual={'package': recovery.PACKAGE, 'version_name': version,
            'version_code': 2, 'target_api': 36}, signature_verified=True,
            architectures=['arm64-v8a'], artifact_sha256=aab_hash)
        archive = self.root / 'fixture.zip'
        with zipfile.ZipFile(archive, 'w') as files:
            files.writestr('release-inputs.json', json.dumps(inputs))
            files.writestr('artifact-manifest.json', json.dumps(evidence))
            files.writestr('Android/Android.aab', payload)
        archive_hash = recovery.digest(archive)
        metadata = {'id': recovery.ARTIFACT_ID, 'expired': False, 'digest': 'sha256:' + archive_hash,
                    'workflow_run': {'id': recovery.SOURCE_RUN, 'head_sha': recovery.SOURCE_SHA, 'head_branch': 'main'}}
        return archive, metadata, archive_hash, aab_hash

    def test_archive_and_aab_hashes_are_independently_verified(self):
        archive, metadata, archive_hash, aab_hash = self.make_archive()
        with patch.object(recovery, 'ARCHIVE_SHA256', archive_hash), patch.object(recovery, 'AAB_SHA256', aab_hash):
            bundle, name = recovery.verify_artifact(archive, metadata, self.root / 'verified')
            self.assertEqual(bundle.read_bytes(), b'fixture bytes')
            self.assertEqual(name, '1.0.2(2)')
            archive.write_bytes(archive.read_bytes() + b'tamper')
            with self.assertRaisesRegex(recovery.StopRecovery, 'archive digest'):
                recovery.verify_artifact(archive, metadata, self.root / 'verified')

    def test_aab_and_release_identity_tampering_are_rejected(self):
        for version, payload in [('1.0.3', b'fixture bytes'), ('1.0.2', b'different AAB')]:
            archive, metadata, archive_hash, aab_hash = self.make_archive(version, payload)
            with patch.object(recovery, 'ARCHIVE_SHA256', archive_hash), patch.object(recovery, 'AAB_SHA256', aab_hash):
                with self.assertRaises(recovery.StopRecovery):
                    recovery.verify_artifact(archive, metadata, self.root / 'verified')

    def test_other_run_or_expired_artifact_is_rejected(self):
        archive, metadata, archive_hash, aab_hash = self.make_archive()
        with patch.object(recovery, 'ARCHIVE_SHA256', archive_hash), patch.object(recovery, 'AAB_SHA256', aab_hash):
            for change in [{'expired': True}, {'id': 1}, {'workflow_run': {'id': 1}}]:
                with self.subTest(change=change), self.assertRaises(recovery.StopRecovery):
                    recovery.verify_artifact(archive, dict(metadata, **change), self.root / 'verified')

    def test_http_upload_transmits_original_bytes_once_without_redirects(self):
        calls = []
        class Session:
            def request(inner, method, url, **kwargs):
                calls.append((method, url, kwargs['data'].read(), kwargs))
                class Response:
                    status_code = 200
                    def json(inner):
                        return {'versionCode': 2, 'sha256': 'fixture-hash'}
                return Response()
        play = recovery.PlayApi.__new__(recovery.PlayApi)
        play.session = Session()
        play.call('POST', '/edit-123/bundles', bundle=self.bundle)
        self.assertEqual(len(calls), 1)
        self.assertEqual(calls[0][2], b'fixture bytes')
        self.assertIn('/upload/androidpublisher/v3/applications/' + recovery.PACKAGE, calls[0][1])
        self.assertFalse(calls[0][3]['allow_redirects'])

    def test_http_error_stops_without_retry_or_raw_response_logging(self):
        calls = []
        class Session:
            def request(inner, *args, **kwargs):
                calls.append(args)
                class Response:
                    status_code = 403
                    def json(inner):
                        raise AssertionError('Error body must not be exposed')
                return Response()
        play = recovery.PlayApi.__new__(recovery.PlayApi)
        play.session = Session()
        with self.assertRaisesRegex(recovery.StopRecovery, 'stop without retry or version increase'):
            play.call('POST', '/edit-123/bundles', bundle=self.bundle)
        self.assertEqual(len(calls), 1)


if __name__ == '__main__':
    unittest.main()
