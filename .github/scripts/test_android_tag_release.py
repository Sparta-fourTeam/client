"""Offline Git/event regressions for the release authorization and source boundary."""
import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import zipfile

SCRIPT = Path(__file__).with_name('android-tag-release.py').resolve()
VERSION_SCRIPT = SCRIPT.with_name('android-release-version.py')
spec = importlib.util.spec_from_file_location('tag_release', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class TagReleaseTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.folder = Path(self.temp.name)
        self.root = self.folder / 'checkout'
        self.root.mkdir()
        self.old_cwd = Path.cwd()
        os.chdir(self.root)
        self.addCleanup(os.chdir, self.old_cwd)
        self.git('init', '-q', '-b', 'main')
        self.git('config', 'user.name', 'Offline release fixture')
        self.git('config', 'user.email', 'release@example.invalid')
        for name in module.PROTECTED_PATHS:
            path = self.root / (name + '/fixture' if name == '.github' else name)
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text('same release infrastructure\n')
        self.policy = self.root / '.github/android-release.json'
        self.policy.write_text(json.dumps({'versionName': '1.0.2', 'versionCode': 2,
            'playUploads': [{'versionName': '1.0.1', 'versionCode': 1}],
            'legacyDraftUploads': [{'versionName': '1.0', 'versionCode': 214011987}]}))
        (self.root / 'game.txt').write_text('common\n')
        self.commit('common')
        self.git('branch', 'dev')
        (self.root / 'main-only.txt').write_text('main fix retained after integration\n')
        self.base = self.commit('main fix')
        self.git('switch', '-q', 'dev')
        (self.root / 'game.txt').write_text('tagged release\n')
        self.source = self.commit('selected dev source')
        self.git('tag', '-a', 'v1.0.3', '-m', 'version fixture')
        self.obj = self.git('rev-parse', 'v1.0.3')
        self.origin = self.folder / 'origin.git'
        self.git('init', '-q', '--bare', str(self.origin))
        self.git('remote', 'add', 'origin', str(self.origin))
        self.git('push', '-q', 'origin', 'main', 'dev', 'refs/tags/v1.0.3')
        self.git('update-ref', 'refs/remotes/origin/main', self.base)
        self.git('update-ref', 'refs/remotes/origin/dev', self.source)
        self.git('switch', '-q', 'main')
        self.git('merge', '--no-ff', '-qm', 'release PR fixture', self.source)
        self.merged = self.git('rev-parse', 'HEAD')
        self.tag_event = dict(repository={'full_name': module.REPOSITORY}, created=True,
                              deleted=False, forced=False, before='0' * 40, after=self.obj,
                              ref='refs/tags/v1.0.3')
        self.tag_env = dict(GITHUB_REPOSITORY=module.REPOSITORY, GITHUB_RUN_ATTEMPT='1',
                            GITHUB_EVENT_NAME='push', GITHUB_REF='refs/tags/v1.0.3',
                            GITHUB_REF_PROTECTED='true', GITHUB_SHA=self.source, GITHUB_RUN_ID='41')
        self.tag_run = dict(id=41, workflow_id=module.WORKFLOW_ID, event='push', head_branch='v1.0.3',
                            head_sha=self.source, run_number=6, run_attempt=1, status='completed',
                            conclusion='success', repository={'full_name': module.REPOSITORY})
        self.run = dict(self.tag_run, id=42, event='pull_request', head_branch='release/v1.0.3', run_number=7)
        self.pr_event = dict(repository={'full_name': module.REPOSITORY}, action='closed',
            pull_request=dict(number=123, merged=True, merge_commit_sha=self.merged,
                head=dict(ref='release/v1.0.3', sha=self.source, repo={'full_name': module.REPOSITORY}),
                base=dict(ref='main', sha=self.base, repo={'full_name': module.REPOSITORY})))
        self.pr_env = dict(self.tag_env, GITHUB_EVENT_NAME='pull_request', GITHUB_REF='refs/heads/main',
                           GITHUB_SHA=self.merged, GITHUB_RUN_ID='42', GITHUB_RUN_NUMBER='7')
        self.request = dict(schema=1, tag='v1.0.3', version_name='1.0.3', tag_object_sha=self.obj,
                            tag_source_sha=self.source, main_base_sha=self.base,
                            tag_run_id=41, workflow_id=module.WORKFLOW_ID, protected_tag=True)

    def git(self, *args):
        return subprocess.check_output(['git', *args], text=True, stderr=subprocess.PIPE).strip()

    def commit(self, message):
        self.git('add', '.')
        self.git('commit', '-qm', message)
        return self.git('rev-parse', 'HEAD')

    def archive(self, request=None):
        stream = io.BytesIO()
        with zipfile.ZipFile(stream, 'w') as zipped:
            zipped.writestr('tag-release-request.json', json.dumps(request or self.request))
        return stream.getvalue()

    def fake_api(self, endpoint, paginate=False, binary=False):
        if endpoint == 'git/ref/tags/v1.0.3':
            obj = self.git('--git-dir=' + str(self.origin), 'rev-parse', 'refs/tags/v1.0.3')
            return {'ref': 'refs/tags/v1.0.3', 'object': {'sha': obj}}
        if '/runs?per_page=' in endpoint:
            return [{'workflow_runs': [self.run, self.tag_run]}]
        if endpoint == 'actions/runs/41/artifacts?per_page=100':
            return [{'artifacts': [dict(id=99, name='tag-release-request', expired=False)]}]
        if endpoint == 'actions/artifacts/99/zip':
            return self.archive()
        raise AssertionError(endpoint)

    def validate_merge(self):
        self.git('update-ref', 'refs/remotes/origin/main', self.merged)
        with patch.object(module, 'api', self.fake_api):
            return module.validate_pr(self.pr_event, self.pr_env)

    def test_new_annotated_tag_request_and_exact_merge_have_distinct_traceable_sources(self):
        with patch.object(module, 'api', self.fake_api):
            self.assertEqual(module.prepare_request(self.tag_event, self.tag_env), self.request)
        self.assertNotEqual(self.obj, self.source)
        result = self.validate_merge()
        self.assertEqual(result['tag_source_sha'], self.source)
        self.assertEqual(result['source_commit'], self.merged)
        self.assertNotEqual(self.source, self.merged)
        self.assertNotEqual(self.git('rev-parse', self.source + '^{tree}'), result['integrated_tree'])
        self.assertEqual(result['integrated_tree'], self.git('rev-parse', self.merged + '^{tree}'))

    def test_tag_updates_deletions_unprotected_refs_and_retries_are_rejected(self):
        for changes in [{'created': False}, {'forced': True}, {'deleted': True},
                        {'before': 'a' * 40}, {'after': self.base}]:
            with self.subTest(changes=changes), patch.object(module, 'api', self.fake_api):
                with self.assertRaises(ValueError):
                    module.prepare_request(dict(self.tag_event, **changes), self.tag_env)
        for changes in [{'GITHUB_REF_PROTECTED': 'false'}, {'GITHUB_RUN_ATTEMPT': '2'}]:
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                module.prepare_request(self.tag_event, dict(self.tag_env, **changes))
        for tag in ['v1.0', 'v01.0.3', '1.0.3', 'v1.0.3-beta', 'v1.0.3\ncode=1']:
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                module.version_from_tag(tag)

    def test_moved_tag_or_second_push_cannot_reuse_original_request(self):
        self.git('--git-dir=' + str(self.origin), 'update-ref', 'refs/tags/v1.0.3', self.base)
        with patch.object(module, 'api', self.fake_api), self.assertRaisesRegex(ValueError, 'moved'):
            module.tag_identity('v1.0.3')
        self.git('--git-dir=' + str(self.origin), 'update-ref', 'refs/tags/v1.0.3', self.obj)
        with patch.object(module, 'api', return_value=[{'workflow_runs': [self.tag_run, dict(self.tag_run, id=99)]}]):
            with self.assertRaisesRegex(ValueError, 'reused'):
                module.tag_run('v1.0.3', self.source)

    def test_newer_dev_is_not_mixed_into_fixed_release_pr(self):
        self.git('switch', '-q', 'dev')
        (self.root / 'game.txt').write_text('newer dev work\n')
        newer = self.commit('after release tag')
        self.git('update-ref', 'refs/remotes/origin/dev', newer)
        self.git('switch', '-q', 'main')
        self.validate_merge()  # The old tagged commit is still the exact source.
        self.pr_event['pull_request']['head']['sha'] = newer
        with self.assertRaisesRegex(ValueError, 'head differs'):
            self.validate_merge()

    def test_pr_checks_reject_main_advance_and_preserve_release_infrastructure(self):
        self.pr_event['action'] = 'opened'
        self.pr_event['pull_request']['merged'] = False
        with patch.object(module, 'api', self.fake_api):
            module.validate_pr(self.pr_event, self.pr_env)
            self.git('update-ref', 'refs/remotes/origin/main', self.merged)
            with self.assertRaisesRegex(ValueError, 'Main advanced'):
                module.validate_pr(self.pr_event, self.pr_env)
        self.git('switch', '-q', 'dev')
        (self.root / '.github/fixture').write_text('old release workflow\n')
        unsafe = self.commit('regress release code')
        with self.assertRaisesRegex(ValueError, 'preserve current main'):
            module.preserve_release_code(self.base, unsafe)

    def test_squash_wrong_tree_fork_unmerged_and_later_main_are_rejected(self):
        tree = self.git('rev-parse', self.merged + '^{tree}')
        for args in [(tree, '-p', self.base),
                     (self.git('rev-parse', self.base + '^{tree}'), '-p', self.base, '-p', self.source)]:
            wrong = self.git('commit-tree', *args, '-m', 'invalid release merge fixture')
            self.git('checkout', '-q', '--detach', wrong)
            self.git('update-ref', 'refs/remotes/origin/main', wrong)
            event = copy.deepcopy(self.pr_event)
            event['pull_request']['merge_commit_sha'] = wrong
            with patch.object(module, 'api', self.fake_api), self.assertRaisesRegex(ValueError, 'plain merge commit'):
                module.validate_pr(event, dict(self.pr_env, GITHUB_SHA=wrong))
        self.git('checkout', '-q', '--detach', self.merged)
        self.git('update-ref', 'refs/remotes/origin/main', self.base)
        with patch.object(module, 'api', self.fake_api), self.assertRaisesRegex(ValueError, 'current main'):
            module.validate_pr(self.pr_event, self.pr_env)
        for field, value in [('merged', False), ('merge_commit_sha', self.source)]:
            event = copy.deepcopy(self.pr_event)
            event['pull_request'][field] = value
            with patch.object(module, 'api', self.fake_api), self.assertRaises(ValueError):
                module.validate_pr(event, self.pr_env)
        event = copy.deepcopy(self.pr_event)
        event['pull_request']['head']['repo']['full_name'] = 'fork/client'
        with self.assertRaises(ValueError):
            module.validate_pr(event, self.pr_env)

    def test_missing_expired_tampered_and_failed_tag_request_stop_before_build(self):
        real_api = self.fake_api
        def expired(endpoint, **kwargs):
            if endpoint.endswith('/artifacts?per_page=100'):
                return [{'artifacts': [dict(id=99, name='tag-release-request', expired=True)]}]
            return real_api(endpoint, **kwargs)
        with patch.object(module, 'api', expired), self.assertRaisesRegex(ValueError, 'expired'):
            module.load_request('v1.0.3', self.source)
        def tampered(endpoint, **kwargs):
            if endpoint.endswith('/99/zip'):
                return self.archive(dict(self.request, tag_object_sha=self.base))
            return real_api(endpoint, **kwargs)
        with patch.object(module, 'api', tampered), self.assertRaisesRegex(ValueError, 'no longer matches'):
            module.load_request('v1.0.3', self.source)
        self.tag_run['conclusion'] = 'failure'
        with patch.object(module, 'api', self.fake_api), self.assertRaisesRegex(ValueError, 'finish successfully'):
            module.load_request('v1.0.3', self.source)

    def test_real_tag_gates_and_version_cli_with_offline_api(self):
        self.git('update-ref', 'refs/remotes/origin/main', self.merged)
        fixture = self.folder / 'api'
        fixture.mkdir()
        values = {'runs': [{'workflow_runs': [self.run, self.tag_run]}], 'run': self.run,
                  'ref': {'ref': 'refs/tags/v1.0.3', 'object': {'sha': self.obj}},
                  'artifacts': [{'artifacts': [dict(id=99, name='tag-release-request', expired=False)]}]}
        for key, value in values.items():
            (fixture / (key + '.json')).write_text(json.dumps(value))
        (fixture / 'request.zip').write_bytes(self.archive())
        fake_gh = fixture / 'gh'
        fake_gh.write_text('#!' + sys.executable + '\n' + '''import os, pathlib, sys
root=pathlib.Path(os.environ['FIXTURE_API'])
e=sys.argv[2]
if '/actions/workflows/' in e: name='runs.json'
elif '/git/ref/tags/' in e: name='ref.json'
elif e.endswith('/actions/runs/42'): name='run.json'
elif e.endswith('/artifacts?per_page=100'): name='artifacts.json'
elif e.endswith('/99/zip'): name='request.zip'
else: raise SystemExit('Unexpected read-only API request: '+e)
sys.stdout.buffer.write((root/name).read_bytes())
''')
        fake_gh.chmod(0o755)
        event_path = self.folder / 'event.json'
        event_path.write_text(json.dumps(self.pr_event))
        output, summary = self.folder / 'output', self.folder / 'summary'
        env = dict(os.environ, **self.pr_env, GITHUB_EVENT_PATH=str(event_path),
                   GITHUB_OUTPUT=str(output), GITHUB_STEP_SUMMARY=str(summary), FIXTURE_API=str(fixture))
        env['PATH'] = str(fixture) + os.pathsep + env['PATH']
        self.git('update-ref', 'refs/remotes/origin/main', self.base)
        event_path.write_text(json.dumps(self.tag_event))
        result = subprocess.run([sys.executable, str(SCRIPT), 'request'],
                                env=dict(env, **self.tag_env), text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn('git branch release/v1.0.3 ' + self.source, summary.read_text())
        self.assertEqual(json.loads((self.root / 'build/tag-release-request.json').read_text()), self.request)
        opened = dict(self.pr_event, action='opened')
        event_path.write_text(json.dumps(opened))
        result = subprocess.run([sys.executable, str(SCRIPT), 'pr'], env=env, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.git('update-ref', 'refs/remotes/origin/main', self.merged)
        event_path.write_text(json.dumps(self.pr_event))
        result = subprocess.run([sys.executable, str(SCRIPT), 'merged'], env=env, text=True, capture_output=True)
        self.assertEqual(result.returncode, 0, result.stderr)
        output.unlink()
        command = [sys.executable, str(VERSION_SCRIPT), '--automatic']
        before = self.policy.read_bytes()
        for extra in [[], ['--expected-code', '300000007']]:
            result = subprocess.run(command + extra, env=env, text=True, capture_output=True)
            self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(output.read_text(), 'version_name=1.0.3\ncode=300000007\n' * 2)
        self.assertEqual(self.policy.read_bytes(), before)
        provenance = json.loads((self.root / 'build/tag-release.json').read_text())
        self.assertEqual(provenance['source_commit'], self.merged)
        self.assertEqual(provenance['tag_source_sha'], self.source)
        for extra, overrides in [(['--version', '1.0.4'], {}), ([], {'GITHUB_RUN_ATTEMPT': '2'})]:
            previous = output.read_bytes()
            result = subprocess.run(command + extra, env=dict(env, **overrides), text=True, capture_output=True)
            self.assertNotEqual(result.returncode, 0)
            self.assertEqual(output.read_bytes(), previous)


if __name__ == '__main__':
    unittest.main()
