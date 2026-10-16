"""Offline regressions for direct-tag provenance, required CI and safe code reuse."""
import copy
import importlib.util
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch
import zipfile
import test_android_tag_release as fixtures

SCRIPT = Path(__file__).with_name('android-tag-build.py').resolve()
spec = importlib.util.spec_from_file_location('direct', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class DirectTagTests(unittest.TestCase):
    def setUp(self):
        self.fx = fixtures.TagReleaseTests()
        self.fx.setUp()
        self.addCleanup(self.fx.doCleanups)
        self.env = dict(self.fx.tag_env, GITHUB_RUN_NUMBER='12')
        self.run = dict(self.fx.tag_run)
        self.old_runs = {1: dict(self.run)}
        self.jobs = {1: [dict(name=name, status='completed', conclusion='skipped', steps=[])
                         for name in ['tag_request', 'prepare', 'build', 'publish', 'manual_scope', 'recover_code2']]}
        self.jobs[1][0].update(conclusion='success', steps=[{'name': 'Offline tests', 'conclusion': 'success'}])
        self.jobs[1][1].update(conclusion='failure', steps=[{'name': 'Required CI', 'conclusion': 'failure'}])
        self.rules, self.checks, self.statuses = [], [], []
        self.calls = []
        self.branch = {'protected': True, 'protection': {'required_status_checks': {'contexts': [], 'checks': []}}}
        self.mock_api = patch.object(module.tag, 'api', side_effect=self.api)
        self.mock_api.start()
        self.addCleanup(self.mock_api.stop)
        self.original = module.request_for_tag(self.fx.tag_event, self.env)

    def api(self, endpoint, paginate=False, binary=False):
        self.calls.append(endpoint)
        f = self.fx
        if endpoint == 'git/ref/tags/v1.0.4':
            return f.fake_api(endpoint)
        if endpoint == 'git/ref/heads/dev':
            return {'object': {'sha': f.source}}
        if endpoint == 'actions/runs/41':
            return self.run
        if endpoint == 'git/ref/heads/main':
            return {'object': {'sha': f.base}}
        if endpoint.startswith('actions/workflows/'):
            return [{'workflow_runs': [self.run] + getattr(self, 'extra_runs', [])}]
        if endpoint == 'actions/runs/41/artifacts?per_page=100':
            return [{'artifacts': [dict(id=99, name='tag-release-request', expired=False)]}]
        if endpoint == 'actions/artifacts/99/zip':
            stream = io.BytesIO()
            with zipfile.ZipFile(stream, 'w') as z:
                z.writestr('tag-release-request.json', json.dumps(self.original))
            return stream.getvalue()
        if '/attempts/' in endpoint:
            attempt = int(endpoint.split('/attempts/')[1].split('/')[0])
            return [{'jobs': self.jobs[attempt]}] if '/jobs?' in endpoint else self.old_runs[attempt]
        if endpoint == 'rules/branches/dev?per_page=100':
            return [self.rules]
        if endpoint == 'branches/dev':
            return self.branch
        if '/check-runs?' in endpoint:
            return [{'check_runs': self.checks}]
        if '/statuses?' in endpoint:
            return [self.statuses]
        raise AssertionError(endpoint)

    def retry(self, attempt=2):
        self.env['GITHUB_RUN_ATTEMPT'] = str(attempt)
        self.run['run_attempt'] = attempt

    def require_ci(self):
        self.rules = [dict(type='required_status_checks', parameters={
            'required_status_checks': [dict(context='Game CI', integration_id=7)]})]
        self.checks = [dict(id=1, name='Game CI', head_sha=self.fx.source, app={'id': 7},
                            status='completed', conclusion='success')]

    def test_direct_source_excludes_main_only_changes_and_has_no_review_or_merge_calls(self):
        before = self.fx.git('rev-parse', 'HEAD')
        result = module.prepare_release(self.original, self.fx.tag_event, self.env)
        self.assertEqual(result['source_commit'], self.fx.source)
        self.assertNotIn('merge_commit_sha', result)
        self.assertNotIn('pr_number', result)
        self.assertEqual(self.fx.git('rev-parse', 'HEAD'), before)
        self.assertFalse(any('pulls' in path or 'comments' in path or path == 'graphql' for path in self.calls))
        self.fx.git('switch', '--detach', '-q', self.fx.source)
        self.assertEqual(module.verify_release(result, dict(self.env, BUILD_SHA=self.fx.source), self.fx.tag_event), result)
        for changes in [{'source_commit': self.fx.merged}, {'tag_source_sha': self.fx.base}, {'retry_policy': 'unknown'}]:
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                module.verify_release(dict(result, **changes), dict(self.env, BUILD_SHA=self.fx.source), self.fx.tag_event)
        with self.assertRaises(ValueError):
            module.verify_release(result, dict(self.env, BUILD_SHA=self.fx.merged), self.fx.tag_event)

    def test_required_ci_uses_exact_sha_app_latest_result_and_classic_protection(self):
        self.require_ci()
        module.required_ci(self.fx.source)
        for change in [dict(status='in_progress', conclusion=None), dict(conclusion='failure'),
                       dict(conclusion='skipped'), dict(head_sha=self.fx.base), dict(app={'id': 99})]:
            original = copy.deepcopy(self.checks)
            self.checks = [dict(original[0], **change)]
            with self.subTest(change=change), self.assertRaisesRegex(ValueError, 'Required CI'):
                module.required_ci(self.fx.source)
            self.checks = original
        self.checks.append(dict(self.checks[0], id=2, status='queued', conclusion=None))
        with self.assertRaises(ValueError):
            module.required_ci(self.fx.source)
        self.rules, self.checks = [], []
        self.statuses = [dict(id=1, context='Classic CI', state='success')]
        with patch.dict(self.branch['protection'], required_status_checks={'contexts': ['Classic CI']}):
            with self.assertRaisesRegex(ValueError, 'App bindings unavailable'):
                module.required_ci(self.fx.source)
        self.checks = [dict(id=1, name='Classic CI', head_sha=self.fx.source, app={'id': 7},
                            status='completed', conclusion='success')]
        with patch.dict(self.branch['protection'], required_status_checks={
                'contexts': ['Classic CI'], 'checks': [{'context': 'Classic CI', 'app_id': 7}]}):
            module.required_ci(self.fx.source)
            self.checks[0]['app']['id'] = 99
            with self.assertRaisesRegex(ValueError, 'Required CI'):
                module.required_ci(self.fx.source)
        with patch.dict(self.branch, protection={}):
            with self.assertRaises(ValueError):
                module.required_ci(self.fx.source)

    def test_direct_scope_rejects_wrong_event_repository_ref_and_counter(self):
        for changes in [dict(created=False), dict(deleted=True), dict(forced=True),
                        dict(before='a' * 40), dict(after=self.fx.base), dict(ref='refs/heads/dev')]:
            with self.subTest(event=changes), self.assertRaises(ValueError):
                module.request_for_tag(dict(self.fx.tag_event, **changes), self.env)
        for changes in [dict(GITHUB_EVENT_NAME='workflow_dispatch'), dict(GITHUB_REPOSITORY='fork/client'),
                        dict(GITHUB_REF_PROTECTED='false'), dict(GITHUB_SHA=self.fx.base),
                        dict(GITHUB_RUN_ID='99'), dict(GITHUB_RUN_NUMBER='13'), dict(GITHUB_RUN_ATTEMPT='0')]:
            with self.subTest(env=changes), self.assertRaises(ValueError):
                module.request_for_tag(self.fx.tag_event, dict(self.env, **changes))
        with patch.dict(self.branch, protected=False), self.assertRaises(ValueError):
            module.prepare_release(self.original, self.fx.tag_event, self.env)
        self.rules = [{'type': 'workflows'}]
        with self.assertRaisesRegex(ValueError, 'Required workflow policy'):
            module.prepare_release(self.original, self.fx.tag_event, self.env)

    def test_retry_allowed_only_before_any_build_or_publish_in_every_attempt(self):
        self.retry()
        self.assertEqual(module.request_for_tag(self.fx.tag_event, self.env), self.original)
        for job_name in ['build', 'publish', 'recover_code2']:
            for conclusion, steps in [('failure', []), ('cancelled', []), ('skipped', [{'name': 'Set up job'}])]:
                jobs = copy.deepcopy(self.jobs)
                row = next(j for j in self.jobs[1] if j['name'] == job_name)
                row.update(conclusion=conclusion, steps=steps)
                with self.subTest(job=job_name, conclusion=conclusion), self.assertRaisesRegex(ValueError, 'already have started'):
                    module.request_for_tag(self.fx.tag_event, self.env)
                self.jobs = jobs
        self.old_runs[2] = dict(self.run, status='completed')
        self.jobs[2] = copy.deepcopy(self.jobs[1])
        self.retry(3)
        self.assertEqual(module.request_for_tag(self.fx.tag_event, self.env), self.original)
        self.jobs[1][2]['conclusion'] = 'success'
        with self.assertRaisesRegex(ValueError, 'already have started'):
            module.request_for_tag(self.fx.tag_event, self.env)

    def test_old_v104_protocol_missing_jobs_newer_run_and_changed_request_cannot_resume(self):
        self.retry()
        for changed in [dict(source_mode='legacy'), dict(retry_policy=None), dict(tag_source_sha=self.fx.base),
                        dict(tag_run_id=99), dict(main_base_sha=self.fx.source)]:
            with self.subTest(changed=changed), patch.dict(self.original, changed), self.assertRaises(ValueError):
                module.request_for_tag(self.fx.tag_event, self.env)
        for changes in [dict(head_sha=self.fx.base), dict(workflow_id=999), dict(run_attempt=2), dict(status='in_progress')]:
            with self.subTest(changes=changes), patch.dict(self.old_runs[1], changes), self.assertRaises(ValueError):
                module.request_for_tag(self.fx.tag_event, self.env)
        self.jobs[1].pop()
        with self.assertRaisesRegex(ValueError, 'incomplete'):
            module.request_for_tag(self.fx.tag_event, self.env)
        self.env['GITHUB_RUN_ATTEMPT'] = '1'
        self.run['run_attempt'] = 1
        self.extra_runs = [dict(self.run, id=42, run_number=13, head_branch='v1.0.5')]
        with self.assertRaisesRegex(ValueError, 'Newer release'):
            module.request_for_tag(self.fx.tag_event, self.env)

    def test_manual_build_only_pins_latest_dev_and_cannot_request_publish_or_recovery(self):
        env = dict(self.env, GITHUB_EVENT_NAME='workflow_dispatch', GITHUB_REF='refs/heads/dev', BUILD_SHA=self.fx.source)
        self.run.update(event='workflow_dispatch', head_branch='dev')
        event = dict(repository={'full_name': module.tag.REPOSITORY}, inputs={
            'build_only': 'true', 'release_version': '1.0.4', 'publish_to_internal': 'false',
            'recover_original_code2': 'false'})
        request = module.manual_build_request(event, env)
        self.assertEqual(request['source_commit'], self.fx.source)
        self.assertNotIn('tag', request)
        self.assertNotIn('merge_commit_sha', request)
        self.assertEqual(module.prepare_release(request, event, env), request)
        self.fx.git('switch', '--detach', '-q', self.fx.source)
        self.assertEqual(module.verify_release(request, env, event), request)
        for flag in ['publish_to_internal', 'recover_original_code2']:
            with self.subTest(flag=flag), patch.dict(event['inputs'], **{flag: 'true'}), self.assertRaises(ValueError):
                module.manual_build_request(event, env)
        for changed in [dict(GITHUB_REF='refs/heads/main'), dict(GITHUB_RUN_ATTEMPT='2'),
                        dict(GITHUB_SHA=self.fx.base), dict(GITHUB_EVENT_NAME='push')]:
            with self.subTest(changed=changed), self.assertRaises(ValueError):
                module.manual_build_request(event, dict(env, **changed))
        version_spec = importlib.util.spec_from_file_location('manual_version', SCRIPT.with_name('android-release-version.py'))
        version = importlib.util.module_from_spec(version_spec)
        version_spec.loader.exec_module(version)
        ledger = json.loads(self.fx.policy.read_text())
        self.run['run_number'] = 13
        env['GITHUB_RUN_NUMBER'] = '13'
        self.assertEqual(version.automatic_release_number(ledger, '1.0.4', ledger, self.run,
            [{'workflow_runs': [self.run]}], env, event=event, release=request), ('1.0.4', 5))
        with self.assertRaises(ValueError):
            version.automatic_release_number(ledger, '1.0.4', ledger, self.run,
                [{'workflow_runs': [self.run]}], dict(env, GITHUB_RUN_ATTEMPT='2'), event=event, release=request)

    def test_real_version_cli_keeps_same_code_after_safe_retry_and_rejects_started_build(self):
        f = self.fx
        f.git('switch', '--detach', '-q', f.source)
        folder = f.folder / 'direct-cli'
        folder.mkdir()
        (folder / 'event.json').write_text(json.dumps(f.tag_event))
        release = module.prepare_release(self.original, f.tag_event, self.env)
        (folder / 'release.json').write_text(json.dumps(release))
        # A fake read-only gh transport exercises the real verifier and allocator
        # together in a subprocess; any unlisted network/mutation fails.
        gh = folder / 'gh'
        gh.write_text('#!' + sys.executable + '\n' + '''import base64,json,os,pathlib,sys
path=next(a for a in sys.argv if a.startswith('repos/')).removeprefix('repos/Sparta-fourTeam/client/')
table=json.loads((pathlib.Path(os.environ['FIXTURE_API'])/'api.json').read_text())
if path not in table: raise SystemExit('Unexpected endpoint: '+path)
row=table[path]
if isinstance(row,dict) and '_zip' in row: sys.stdout.buffer.write(base64.b64decode(row['_zip']))
else: print(json.dumps(row))
''')
        gh.chmod(0o755)
        env = dict(os.environ, **self.env, BUILD_SHA=f.source, FIXTURE_API=str(folder),
                   GITHUB_EVENT_PATH=str(folder / 'event.json'), GITHUB_OUTPUT=str(folder / 'output'),
                   GITHUB_STEP_SUMMARY=str(folder / 'summary'))
        env['PATH'] = str(folder) + os.pathsep + env['PATH']
        command = [sys.executable, str(SCRIPT.with_name('android-release-version.py')), '--automatic',
                   '--release-request', str(folder / 'release.json'), '--expected-code', '4']
        import base64
        def run_cli():
            values = {
                'git/ref/tags/v1.0.4': self.api('git/ref/tags/v1.0.4'),
                'git/ref/heads/main': self.api('git/ref/heads/main'),
                f'actions/workflows/{module.tag.WORKFLOW_ID}/runs?per_page=100': self.api('actions/workflows/x'),
                'actions/runs/41': self.run, 'rules/branches/dev?per_page=100': [[]], 'branches/dev': self.branch,
                'actions/runs/41/artifacts?per_page=100': self.api('actions/runs/41/artifacts?per_page=100'),
                'actions/artifacts/99/zip': {'_zip': base64.b64encode(self.api('actions/artifacts/99/zip')).decode()}}
            for prior in range(1, int(env['GITHUB_RUN_ATTEMPT'])):
                values[f'actions/runs/41/attempts/{prior}'] = self.old_runs[prior]
                values[f'actions/runs/41/attempts/{prior}/jobs?per_page=100'] = [{'jobs': self.jobs[prior]}]
            (folder / 'api.json').write_text(json.dumps(values))
            return subprocess.run(command, env=env, capture_output=True, text=True)
        result = run_cli()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.retry()
        env['GITHUB_RUN_ATTEMPT'] = '2'
        result = run_cli()
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual((folder / 'output').read_text(), 'version_name=1.0.4\ncode=4\n' * 2)
        self.jobs[1][2]['conclusion'] = 'success'
        before = (folder / 'output').read_text()
        self.assertNotEqual(run_cli().returncode, 0)
        self.assertEqual((folder / 'output').read_text(), before)


if __name__ == '__main__':
    unittest.main()
