import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import textwrap
import unittest

SCRIPT = Path(__file__).with_name('android-release-version.py').resolve()
spec = importlib.util.spec_from_file_location('release_version', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def policy(code=1, version='1.0.1', released=None):
    return {'versionName': version, 'versionCode': code, 'playUploads': released or [],
            'legacyDraftUploads': [{'versionName': '1.0', 'versionCode': 214011987}]}


class ReleaseNumberTests(unittest.TestCase):
    def test_first_candidate_and_rebuild_keep_code_one(self):
        self.assertEqual(module.release_number(policy(), '1.0.1'), ('1.0.1', 1))
        self.assertEqual(module.release_number(policy(), '1.0.1', policy()), ('1.0.1', 1))

    def test_next_confirmed_upload_increments_by_one(self):
        released = [{'versionName': '1.0.1', 'versionCode': 1}]
        self.assertEqual(module.release_number(policy(2, '1.0.2', released), '1.0.2', policy()), ('1.0.2', 2))

    def test_stale_checkout_cannot_reuse_code_uploaded_on_main(self):
        main = policy(2, '1.0.2', [{'versionName': '1.0.1', 'versionCode': 1}])
        with self.assertRaises(ValueError):
            module.release_number(policy(), '1.0.1', main)

    def test_accepted_legacy_draft_code_is_unavailable_without_raising_the_floor(self):
        self.assertEqual(module.release_number(policy(), '1.0.1'), ('1.0.1', 1))
        used = policy()
        used['legacyDraftUploads'].append({'versionName': '0.9.0', 'versionCode': 1})
        with self.assertRaises(ValueError):
            module.release_number(policy(), '1.0.1', used)

    def test_historical_apk_requires_exact_record_while_play_reuse_is_rejected(self):
        main = policy(2, '1.0.2', [{'versionName': '1.0.1', 'versionCode': 1}])
        self.assertEqual(module.release_number(policy(), '1.0.1', main, apk_build=True), ('1.0.1', 1))
        with self.assertRaises(ValueError):
            module.release_number(policy(), '1.0.1', main)
        with self.assertRaises(ValueError):
            module.release_number(policy(1, '1.0.0'), '1.0.0', main, apk_build=True)
        with self.assertRaises(ValueError):
            module.release_number(policy(2, '1.0.1'), '1.0.1', main, apk_build=True)

    def test_code_skip_gap_duplicate_and_conflict_are_rejected(self):
        invalid = [policy(2), policy(True), policy(214011987),
                   policy(3, released=[{'versionName': '1.0.0', 'versionCode': 2}]),
                   policy(2, released=[{'versionName': '1.0.0', 'versionCode': 1}] * 2)]
        for p in invalid:
            with self.subTest(p=p), self.assertRaises(ValueError):
                module.release_number(p, '1.0.1')
        with self.assertRaises(ValueError):
            module.release_number(policy(2, released=[{'versionName': '1.0.0', 'versionCode': 1}]), '1.0.1',
                                  policy(2, released=[{'versionName': '0.9.0', 'versionCode': 1}]))

    def test_requested_version_must_match_and_unuploaded_candidate_can_change_name(self):
        with self.assertRaises(ValueError):
            module.release_number(policy(), '1.0')
        self.assertEqual(module.release_number(policy(1, '1.0.2'), '1.0.2', policy()), ('1.0.2', 1))

    def test_union_cannot_hide_a_gap_in_either_ledger(self):
        rows = [{'versionName': f'1.0.{n}', 'versionCode': n} for n in [1, 2, 3]]
        local_gap = policy(4, '1.0.4', [rows[0], rows[2]])
        main_contiguous = policy(3, '1.0.3', rows[:2])
        with self.assertRaises(ValueError):
            module.release_number(local_gap, '1.0.4', main_contiguous)
        local_contiguous = policy(4, '1.0.4', rows)
        main_gap = policy(4, '1.0.4', [rows[0], rows[2]])
        with self.assertRaises(ValueError):
            module.release_number(local_contiguous, '1.0.4', main_gap)

    def test_explicit_preserved_build_reservation_skips_only_reserved_codes(self):
        rows = [{'versionName': '1.0.1', 'versionCode': 1}, {'versionName': '1.0.2', 'versionCode': 2}]
        candidate = policy(4, '1.0.3', rows)
        with self.assertRaises(ValueError):
            module.release_number(candidate, '1.0.3')
        candidate['reservedBuilds'] = [{'versionCode': 3}]
        self.assertEqual(module.release_number(candidate, '1.0.3'), ('1.0.3', 4))
        for reserved in [True, 0, 1, 214011987, 2100000001, 4]:
            candidate['reservedBuilds'] = [{'versionCode': reserved}]
            with self.subTest(reserved=reserved), self.assertRaises(ValueError):
                module.release_number(candidate, '1.0.3')

    def test_cli_ignores_old_high_code_build_tag_and_checks_new_tag_tree(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            def git(*args):
                return subprocess.check_output(['git', '-C', folder, *args], text=True).strip()
            git('init', '-q')
            git('config', 'user.name', 'Release policy test')
            git('config', 'user.email', 'release-policy-test@example.invalid')
            (root / '.github').mkdir()
            (root / '.github/android-release.json').write_text(json.dumps(policy()))
            git('add', '.')
            git('commit', '-qm', 'test fixture')
            git('update-ref', 'refs/remotes/origin/main', 'HEAD')
            git('tag', '-a', 'v1.0', '-m', 'Android-Version-Code: 214011987')
            output = root / 'output.txt'
            env = dict(os.environ, GITHUB_OUTPUT=str(output))
            result = subprocess.run(['python3', str(SCRIPT), '--version', '1.0.1'], cwd=root, env=env, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertEqual(output.read_text(), 'version_name=1.0.1\ncode=1\n')
            git('tag', '-a', 'v1.0.1', '-m', 'candidate build receipt')
            same_tree = subprocess.run(['python3', str(SCRIPT), '--version', 'v1.0.1'], cwd=root, env=env, capture_output=True)
            self.assertEqual(same_tree.returncode, 0, same_tree.stderr)
            (root / 'changed.txt').write_text('different candidate source')
            git('add', '.')
            git('commit', '-qm', 'different tree')
            mismatch = subprocess.run(['python3', str(SCRIPT), '--version', '1.0.1'], cwd=root, env=env, capture_output=True)
            self.assertNotEqual(mismatch.returncode, 0)

    def test_tagged_apk_cli_rebuild_after_main_records_upload(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            def git(*args):
                return subprocess.check_output(['git', '-C', folder, *args], text=True).strip()
            git('init', '-q')
            git('config', 'user.name', 'Release policy test')
            git('config', 'user.email', 'release-policy-test@example.invalid')
            (root / '.github').mkdir()
            config = root / '.github/android-release.json'
            config.write_text(json.dumps(policy()))
            git('add', '.')
            git('commit', '-qm', 'candidate fixture')
            git('tag', '-a', 'v1.0.1', '-m', 'candidate receipt')
            config.write_text(json.dumps(policy(2, '1.0.2', [{'versionName': '1.0.1', 'versionCode': 1}])))
            git('add', '.')
            git('commit', '-qm', 'confirmed upload fixture')
            git('update-ref', 'refs/remotes/origin/main', 'HEAD')
            git('switch', '--detach', '-q', 'v1.0.1')
            output = root / 'output.txt'
            env = dict(os.environ, GITHUB_OUTPUT=str(output))
            command = ['python3', str(SCRIPT), '--version', 'v1.0.1']
            apk = subprocess.run(command + ['--apk-build'], cwd=root, env=env, capture_output=True)
            self.assertEqual(apk.returncode, 0, apk.stderr)
            self.assertEqual(output.read_text(), 'version_name=1.0.1\ncode=1\n')
            before = output.read_bytes()
            play = subprocess.run(command, cwd=root, env=env, capture_output=True)
            self.assertNotEqual(play.returncode, 0)
            self.assertEqual(output.read_bytes(), before)
