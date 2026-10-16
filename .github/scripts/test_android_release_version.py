import hashlib
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import textwrap
import unittest
from unittest.mock import patch

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

    def test_historical_apk_accepts_confirmed_automatic_gap_only_with_valid_allocation(self):
        rows = [dict(versionName=f'1.0.{n}', versionCode=n) for n in [1, 2, 3]]
        row = dict(versionName='1.0.5', versionCode=6,
                   versionCodeAllocation=module.allocation_receipt(14))
        ledger = policy(6, '1.0.5', rows + [row])
        self.assertEqual(module.release_number(ledger, '1.0.5', apk_build=True), ('1.0.5', 6))
        row['versionCodeAllocation']['runNumber'] = 15
        with self.assertRaises(ValueError):
            module.release_number(ledger, '1.0.5', apk_build=True)

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


class AutomaticReleaseNumberTests(unittest.TestCase):
    def setUp(self):
        self.env = dict(GITHUB_REPOSITORY='Sparta-fourTeam/client', GITHUB_RUN_ID='42',
                        GITHUB_RUN_NUMBER='12', GITHUB_RUN_ATTEMPT='1', GITHUB_SHA='tag-source', BUILD_SHA='fixture-sha',
                        GITHUB_EVENT_NAME='push', GITHUB_REF='refs/tags/v1.0.4')
        self.run = dict(id=42, workflow_id=378016847, run_number=12, run_attempt=1,
                        event='push', head_sha='tag-source',
                        repository={'full_name': 'Sparta-fourTeam/client'})
        self.pages = [{'workflow_runs': [self.run]}]
        self.event = {'created': True, 'forced': False, 'deleted': False}
        self.release = {'tag': 'v1.0.4', 'tag_source_sha': 'tag-source', 'merge_commit_sha': 'fixture-sha', 'tag_run_id': 42}


    def allocate(self, ledger=None, main=None, version='1.0.4', expected_code=None):
        event = json.loads(json.dumps(self.event))
        release = dict(self.release, tag='v' + version)
        env = dict(self.env, GITHUB_REF='refs/tags/v' + version)
        return module.automatic_release_number(ledger or self.ledger(), version, main or self.ledger(),
                                               self.run, self.pages, env, expected_code, event, release)

    def ledger(self):
        return policy(3, '1.0.3', [dict(versionName=f'1.0.{n}', versionCode=n,
                      actionsRun=38832543309 if n == 3 else n) for n in [1, 2, 3]])

    def test_small_codes_start_after_published_anchor_not_old_builds_or_large_draft(self):
        ledger = self.ledger()
        ledger['versionCode'] = 4  # Historical build-only candidate is not a publication.
        ledger['reservedBuilds'] = [{'versionCode': 4}]
        before = json.dumps(ledger)
        self.assertEqual(self.allocate(ledger), ('1.0.4', 4))
        self.assertEqual(json.dumps(ledger), before)
        self.run['run_number'] = 13
        self.env['GITHUB_RUN_NUMBER'] = '13'
        self.assertEqual(self.allocate(ledger), ('1.0.4', 5))
        for number in [0, 11, -1, True]:
            with self.subTest(number=number), self.assertRaises(ValueError):
                module.candidate_code(ledger, '1.0.4', ledger, number)
        with self.assertRaisesRegex(ValueError, 'anchor'):
            self.allocate(main=policy())

    def test_rerun_and_new_workflow_counter_fail_closed(self):
        for overrides in [{'run_attempt': 2}, {'workflow_id': 999},
                          {'repository': {'full_name': 'fork/client'}},
                          {'event': 'pull_request'}, {'head_sha': 'other-source'}, {'run_number': 1}]:
            with self.subTest(overrides=overrides), patch.dict(self.run, overrides):
                with self.assertRaises(ValueError):
                    self.allocate()
        with patch.dict(self.env, GITHUB_RUN_ATTEMPT='2'):
            with self.assertRaisesRegex(ValueError, 'Re-run'):
                self.allocate()

    def test_newer_dispatch_on_another_page_rejects_old_queued_run(self):
        self.pages.append({'workflow_runs': [dict(self.run, id=43, run_number=13, conclusion='failure')]})
        with self.assertRaisesRegex(ValueError, 'newer release'):
            self.allocate()
        self.pages = [{'workflow_runs': []}]
        with self.assertRaisesRegex(ValueError, 'missing'):
            self.allocate()

    def test_used_code_and_published_floor_checked_in_both_ledgers(self):
        for key, used_code in [('playUploads', 4), ('playUploads', 5), ('legacyDraftUploads', 4)]:
            used = self.ledger()
            used[key].append({'versionName': '0.9', 'versionCode': used_code})
            for kwargs in [{'ledger': used}, {'main': used}]:
                with self.subTest(key=key, kwargs=kwargs), self.assertRaises(ValueError):
                    self.allocate(**kwargs)
        with self.assertRaises(ValueError):
            self.allocate(version='1.0.3')
        with self.assertRaisesRegex(ValueError, 'main release ledger'):
            module.automatic_release_number(self.ledger(), '1.0.4', None, self.run, self.pages,
                                            self.env, event=self.event, release=self.release)

    def test_play_boundaries_and_artifact_code_binding(self):
        self.assertEqual(self.allocate(expected_code=4), ('1.0.4', 4))
        with self.assertRaisesRegex(ValueError, 'artifact code'):
            self.allocate(expected_code=5)
        self.run['run_number'] = 2100000008
        self.env['GITHUB_RUN_NUMBER'] = '2100000008'
        self.assertEqual(self.allocate(), ('1.0.4', 2100000000))
        self.run['run_number'] += 1
        self.env['GITHUB_RUN_NUMBER'] = '2100000009'
        with self.assertRaisesRegex(ValueError, 'Google Play limit'):
            self.allocate()
        self.run['run_number'] = 12
        self.env['GITHUB_RUN_NUMBER'] = '12'
        self.assertEqual(len(f'{self.allocate(version="1" * 45 + ".0")[0]}(4)'), 50)
        for version in ['v1.0.4', '1.0.3\ncode=1', '1', '1' * 46 + '.0']:
            with self.subTest(version=version), self.assertRaises(ValueError):
                self.allocate(version=version)

    def test_manual_dispatch_cannot_allocate_an_automatic_code(self):
        with patch.dict(self.run, event='workflow_dispatch'):
            with self.assertRaises(ValueError):
                self.allocate()
        with patch.dict(self.event, created=False):
            with self.assertRaises(ValueError):
                self.allocate()
        self.release['merge_commit_sha'] = 'different-merge'
        with self.assertRaises(ValueError):
            self.allocate()


class PlayReleaseNameTests(unittest.TestCase):
    def run_artifact_preflight(self, version='1.0.2', code=2, requested_version=None, expected_code=None,
                               expected_pr=123):
        # Execute the workflow's real pre-upload check without Play access or an AAB upload.
        workflow = SCRIPT.parent.parent / 'workflows/deploy-google-play.yml'
        step = workflow.read_text().split('      - name: Recheck artifact identity\n', 1)[1]
        step = step.split('      - name:', 1)[0]
        script = textwrap.dedent(step.split("          python3 - <<'PY'\n", 1)[1].rsplit('          PY', 1)[0])
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            bundle = root / 'verified/Android/test.aab'
            bundle.parent.mkdir(parents=True)
            bundle.write_bytes(b'local preflight fixture')
            evidence = {'source_commit': 'fixture-sha', 'pr_number': 123, 'signature_verified': True,
                        'artifact_sha256': hashlib.sha256(bundle.read_bytes()).hexdigest(),
                        'actual': {'package': 'com.teamdoodle.doodledefense',
                                   'version_name': version, 'version_code': code}}
            (root / 'verified/artifact-manifest.json').write_text(json.dumps(evidence))
            (root / 'build').mkdir()
            (root / 'release-context').mkdir()
            (root / 'release-context/tag-release.json').write_text(json.dumps({'source_commit': 'fixture-sha',
                                                                    'pr_number': expected_pr}))
            output = root / 'output.txt'
            env = dict(os.environ, GITHUB_SHA='tag-source', BUILD_SHA='fixture-sha', GITHUB_OUTPUT=str(output),
                       RELEASE_VERSION=version if requested_version is None else requested_version,
                       ANDROID_VERSION_CODE=str(code if expected_code is None else expected_code))
            result = subprocess.run(['python3', '-c', script], cwd=root, env=env, capture_output=True, text=True)
            return result, output.read_text() if output.exists() else ''

    def test_verified_aab_version_and_code_form_release_name(self):
        result, output = self.run_artifact_preflight()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(output, 'release_name=1.0.2(2)\n')
        workflow = (SCRIPT.parent.parent / 'workflows/deploy-google-play.yml').read_text()
        self.assertIn('python3 .github/scripts/publish-play-internal.py', workflow)

    def test_release_name_accepts_50_characters_and_rejects_51_before_upload(self):
        result, output = self.run_artifact_preflight(version='1' * 45 + '.0')
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(len(output.removeprefix('release_name=').strip()), 50)
        result, output = self.run_artifact_preflight(version='1' * 46 + '.0')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Play release name exceeds 50 characters', result.stderr)
        self.assertEqual(output, '')

    def test_artifact_version_mismatch_cannot_produce_publish_name(self):
        for overrides in [{'requested_version': '1.0.3'}, {'expected_code': 3}]:
            with self.subTest(overrides=overrides):
                result, output = self.run_artifact_preflight(**overrides)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn('Artifact version mismatch', result.stderr)
                self.assertEqual(output, '')

    def test_artifact_from_another_release_pr_cannot_publish(self):
        result, output = self.run_artifact_preflight(expected_pr=456)
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('Artifact tag/source provenance mismatch', result.stderr)
        self.assertEqual(output, '')


if __name__ == '__main__':
    unittest.main()
