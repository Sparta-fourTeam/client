"""No network/mutations: mock the future GitHub PR/review/merge protocol."""
import copy
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import unittest
from unittest.mock import patch
import test_android_tag_release as fixtures

SCRIPT = Path(__file__).with_name('android-release-integration.py').resolve()
spec = importlib.util.spec_from_file_location('integration', SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class IntegrationTests(unittest.TestCase):
    def setUp(self):
        self.fx = fixtures.TagReleaseTests()
        self.fx.setUp()
        self.addCleanup(self.fx.doCleanups)
        f = self.fx
        self.env = dict(f.tag_env, GITHUB_RUN_NUMBER='6')
        self.calls, self.merged, self.created = [], False, False
        self.rows = [dict(id=11, user=dict(id=module.CODEX_ID, login=module.CODEX_LOGIN, type='Bot'),
                          commit_id=f.source, state='COMMENTED', submitted_at='2026-10-15T00:00:00Z', body='Review complete')]
        self.state = dict(headRefOid=f.source, baseRefOid=f.base, headRefName='release/v1.0.3', baseRefName='main',
                          isDraft=False, state='OPEN', mergeable='MERGEABLE', mergeStateStatus='CLEAN',
                          reviewDecision=None, threads=[])
        self.pr = dict(number=123, head=dict(ref='release/v1.0.3', sha=f.source, repo={'full_name':module.REPOSITORY}),
                       base=dict(ref='main', sha=f.base, repo={'full_name':module.REPOSITORY}), draft=False,
                       state='open', merged=False, merge_commit_sha=None)
        self.api_patch = patch.object(module, 'api', self.api)
        self.api_patch.start()
        self.addCleanup(self.api_patch.stop)
        self.tag_patch = patch.object(module.tag, 'api', self.tag_api)
        self.tag_patch.start()
        self.addCleanup(self.tag_patch.stop)

    def tag_api(self, endpoint, paginate=False, binary=False):
        if endpoint == 'issues/123/comments?per_page=100':
            return [getattr(self, 'summary_comments', [])]
        if endpoint.startswith('actions/runs?head_sha='):
            return [{'workflow_runs':getattr(self, 'approval_runs', [])}]
        if '/runs?per_page=' in endpoint:
            return [{'workflow_runs':[self.fx.tag_run]}]
        if endpoint.startswith('pulls/123/reviews'):
            return [copy.deepcopy(self.rows)]
        return self.fx.fake_api(endpoint, paginate, binary)

    def api(self, method, endpoint, payload=None, missing_ok=False):
        self.calls.append((method, endpoint, copy.deepcopy(payload)))
        f = self.fx
        if endpoint == 'git/ref/heads/main':
            return {'object':{'sha': f.merged if self.merged else f.base}}
        if endpoint == 'git/ref/heads/release/v1.0.3':
            return {'object':{'sha':f.source}} if self.created else None
        if endpoint == 'git/refs' and method == 'POST':
            self.created = True
            return {'ref':payload['ref'], 'object':{'sha':payload['sha']}}
        if endpoint.startswith('pulls?'):
            return []
        if endpoint == 'pulls' and method == 'POST':
            return copy.deepcopy(self.pr)
        if endpoint == 'issues/123/comments' and method == 'POST':
            return {'id':55}
        if endpoint == 'pulls/123':
            pr = copy.deepcopy(self.pr)
            if self.merged:
                pr.update(state='closed', merged=True, merge_commit_sha=f.merged)
            return pr
        if endpoint == 'graphql':
            s = copy.deepcopy(self.state)
            s['reviewThreads'] = {'nodes':s.pop('threads'), 'pageInfo':{'hasNextPage':False, 'endCursor':None}}
            return {'data':{'repository':{'pullRequest':s}}}
        if endpoint == 'pulls/123/merge' and method == 'PUT':
            self.assertEqual(payload, {'sha':f.source, 'merge_method':'merge'})
            self.merged = True
            return {'merged':True, 'sha':f.merged}
        if endpoint == 'git/commits/' + f.merged:
            return {'parents':[{'sha':f.base},{'sha':f.source}],
                    'tree':{'sha':f.git('rev-parse',f.merged+'^{tree}')}}
        if endpoint == 'commits/' + f.source[:7]:
            return {'sha':f.source}
        raise AssertionError((method,endpoint,payload))

    def integrate(self, **kwargs):
        return module.integrate(self.fx.request, self.env, self.fx.tag_event, timeout=0, sleep=lambda _: None, **kwargs)

    def merge_calls(self):
        return [c for c in self.calls if c[0] == 'PUT' and c[1].endswith('/merge')]

    def test_only_reviewed_fixed_head_merges_and_actual_merge_sha_is_built(self):
        before = self.fx.policy.read_bytes()
        receipt = self.integrate()
        self.assertEqual(len(self.merge_calls()),1)
        self.assertEqual(receipt['source_commit'],self.fx.merged)
        self.assertEqual(receipt['tag_source_sha'],self.fx.source)
        self.assertEqual(receipt['codex_commit_sha'],self.fx.source)
        self.assertEqual(self.fx.policy.read_bytes(),before)
        self.assertFalse(any('reviews' in c[1] or 'dismiss' in c[1] for c in self.calls if c[0]!='GET'))
        module.verify_release(receipt, dict(self.env, BUILD_SHA=self.fx.merged), self.fx.tag_event)
        with self.assertRaisesRegex(ValueError,'checkout'):
            module.verify_release(receipt, dict(self.env, BUILD_SHA=self.fx.source), self.fx.tag_event)

    def test_missing_stale_pending_dismissed_spoofed_and_blocking_codex_reviews_never_merge(self):
        original = copy.deepcopy(self.rows[0])
        invalid = [None, dict(original, commit_id=self.fx.base), dict(original,state='PENDING'),
                   dict(original,state='DISMISSED'), dict(original,state='CHANGES_REQUESTED'),
                   dict(original,body='[P1] unsafe release'), dict(original,submitted_at=None),
                   dict(original,user=dict(id=123,login=module.CODEX_LOGIN,type='Bot'))]
        for row in invalid:
            with self.subTest(row=row):
                self.rows = [] if row is None else [row]
                with self.assertRaisesRegex(ValueError,'Timed out'):
                    self.integrate()
                self.assertFalse(self.merge_calls())

    def test_required_ci_unresolved_threads_and_independent_changes_requested_block_merge(self):
        for change in [{'mergeStateStatus':'BLOCKED'}, {'reviewDecision':'REVIEW_REQUIRED'},
                       {'threads':[{'isResolved':False}]}, {'isDraft':True}, {'mergeable':'UNKNOWN'}]:
            with self.subTest(change=change), patch.dict(self.state,change):
                with self.assertRaisesRegex(ValueError,'Timed out'):
                    self.integrate()
                self.assertFalse(self.merge_calls())
        self.rows.append(dict(self.rows[0],id=12,user={'id':456,'login':'reviewer','type':'User'},state='CHANGES_REQUESTED'))
        with self.assertRaisesRegex(ValueError,'Timed out'):
            self.integrate()
        self.assertFalse(self.merge_calls())

    def test_pending_workflow_execution_approval_never_merges_even_without_required_ci(self):
        self.approval_runs = [dict(head_sha=self.fx.source, status='waiting', conclusion=None,
                                  pull_requests=[{'number':123}])]
        with self.assertRaisesRegex(ValueError, 'Approve workflows to run'):
            self.integrate()
        self.assertFalse(self.merge_calls())
        attempt = json.loads(Path('build/release-integration-attempt.json').read_text())
        self.assertEqual(attempt['phase'], 'awaiting_workflow_approval')
        self.assertFalse(any('/approve' in c[1] for c in self.calls))
        self.approval_runs[0].update(status='completed', conclusion='success')
        self.assertEqual(self.integrate()['merge_commit_sha'], self.fx.merged)

    def test_mutated_pr_head_or_main_base_never_merges(self):
        with patch.dict(self.state,headRefOid=self.fx.base):
            with self.assertRaisesRegex(ValueError,'head moved'):
                self.integrate()
        with patch.dict(self.state,baseRefOid=self.fx.source):
            with self.assertRaisesRegex(ValueError,'base changed'):
                self.integrate()
        with patch.dict(self.pr['head'],sha=self.fx.base):
            with self.assertRaisesRegex(ValueError,'mismatch'):
                self.integrate()
        self.assertFalse(self.merge_calls())

    def summary_comment(self, **changes):
        body = ('<!-- codex-pull-request-review-summary -->\n'
                '| 📝 **Code Review** | ✅ **Completed** '
                '<relative-time datetime="2026-10-15T00:02:00Z">done</relative-time> '
                f'| `{self.fx.source[:7]}` | PR opened |')
        return dict(id=22, user=dict(id=module.CODEX_ID, login=module.CODEX_LOGIN, type='Bot'),
                    body=body, updated_at='2026-10-15T00:02:01Z', **changes)

    def test_no_findings_bot_summary_resolves_exact_full_sha_without_formal_review(self):
        self.rows = []
        self.summary_comments = [self.summary_comment()]
        receipt = self.integrate()
        self.assertEqual(receipt['codex_review_kind'], 'summary')
        self.assertEqual(receipt['codex_review_id'], 22)
        self.assertEqual(receipt['codex_commit_sha'], self.fx.source)

    def test_summary_cannot_replace_actor_identity_exact_source_or_completed_state(self):
        self.rows = []
        original = self.summary_comment()
        invalid = [dict(original, user=dict(id=123, login=module.CODEX_LOGIN, type='Bot')),
                   dict(original, body=original['body'].replace('Completed', 'Running')),
                   dict(original, body=original['body'] + '\n<!-- codex-review-state:v1 running -->')]
        for row in invalid:
            self.summary_comments = [row]
            with self.assertRaisesRegex(ValueError, 'Timed out'):
                self.integrate()
            self.assertFalse(self.merge_calls())
        self.summary_comments = [original]
        saved = self.api
        def different(method, endpoint, payload=None, missing_ok=False):
            if endpoint.startswith('commits/'):
                return {'sha':self.fx.base}
            return saved(method, endpoint, payload, missing_ok)
        with patch.object(module, 'api', different), self.assertRaisesRegex(ValueError, 'Timed out'):
            self.integrate()
        self.assertFalse(self.merge_calls())

    def test_completed_summary_never_clears_unresolved_threads_or_change_requests(self):
        self.summary_comments = [self.summary_comment()]
        self.rows = []
        with patch.dict(self.state, threads=[{'isResolved':False}]), self.assertRaisesRegex(ValueError, 'Timed out'):
            self.integrate()
        self.rows = [dict(id=90, user={'id':456}, commit_id=self.fx.source, state='CHANGES_REQUESTED',
                          submitted_at='2026-10-15T00:01:00Z')]
        with self.assertRaisesRegex(ValueError, 'Timed out'):
            self.integrate()
        self.assertFalse(self.merge_calls())

    def test_denied_or_uncertain_merge_is_not_retried_and_has_no_receipt(self):
        saved = self.api
        def denied(method,endpoint,payload=None,missing_ok=False):
            if method=='PUT':
                self.calls.append((method,endpoint,payload))
                raise ValueError('GitHub request failed: protected merge denied')
            return saved(method,endpoint,payload,missing_ok)
        with patch.object(module,'api',denied), self.assertRaisesRegex(ValueError,'denied'):
            self.integrate()
        self.assertEqual(len(self.merge_calls()),1)
        self.assertFalse(self.merged)
        self.calls.clear()
        def uncertain(method,endpoint,payload=None,missing_ok=False):
            if method=='PUT':
                self.calls.append((method,endpoint,payload))
                return {'merged':False,'message':'unknown'}
            return saved(method,endpoint,payload,missing_ok)
        with patch.object(module,'api',uncertain), self.assertRaisesRegex(ValueError,'not confirmed'):
            self.integrate()
        self.assertEqual(len(self.merge_calls()),1)

    def test_existing_wrong_release_branch_is_never_overwritten(self):
        saved = self.api
        def wrong(method,endpoint,payload=None,missing_ok=False):
            if endpoint=='git/ref/heads/release/v1.0.3':
                return {'object':{'sha':self.fx.base}}
            return saved(method,endpoint,payload,missing_ok)
        with patch.object(module,'api',wrong), self.assertRaisesRegex(ValueError,'never force'):
            self.integrate()
        self.assertFalse(any(c[0]!='GET' for c in self.calls))

    def test_review_thread_pagination_cannot_hide_a_blocker(self):
        def pages(method,endpoint,payload=None,missing_ok=False):
            state = copy.deepcopy(self.state)
            last = payload['variables']['cursor'] is not None
            state.pop('threads')
            state['reviewThreads']={'nodes':[{'isResolved':not last}],
                                   'pageInfo':{'hasNextPage':not last,'endCursor':'page2' if not last else None}}
            return {'data':{'repository':{'pullRequest':state}}}
        with patch.object(module,'api',pages):
            state=module.review_state(123)
        self.assertEqual(len(state['threads']),2)
        self.assertIsNone(module.review_gate(self.fx.request,self.rows,state))

    def test_preflight_rejection_happens_before_any_repository_write(self):
        with patch.object(module.tag,'prepare_request',return_value=dict(self.fx.request,main_base_sha=self.fx.source)):
            with self.assertRaisesRegex(ValueError,'Main/request changed'):
                self.integrate()
        self.assertFalse(any(c[0]!='GET' for c in self.calls))

    def test_newer_codex_review_for_another_commit_cannot_fall_back_to_old_pass(self):
        self.rows.append(dict(self.rows[0], id=12, commit_id=self.fx.base,
                              submitted_at='2026-10-15T00:01:00Z'))
        with self.assertRaisesRegex(ValueError,'Timed out'):
            self.integrate()
        self.assertFalse(self.merge_calls())

    def test_comment_does_not_clear_an_independent_change_request(self):
        actor={'id':456,'login':'reviewer','type':'User'}
        self.rows += [dict(self.rows[0],id=12,user=actor,state='CHANGES_REQUESTED'),
                      dict(self.rows[0],id=13,user=actor,state='COMMENTED',submitted_at='2026-10-15T00:01:00Z')]
        self.assertIsNone(module.review_gate(self.fx.request,self.rows,self.state))
        self.rows.append(dict(self.rows[0],id=14,user=actor,state='APPROVED',submitted_at='2026-10-15T00:02:00Z'))
        self.assertIsNotNone(module.review_gate(self.fx.request,self.rows,self.state))

    def test_real_build_version_cli_uses_receipt_merge_sha_and_original_tag_run_number(self):
        receipt = self.integrate()
        f = self.fx
        f.git('update-ref','refs/remotes/origin/main',f.merged)
        folder = f.folder / 'cli-api'
        folder.mkdir()
        state = copy.deepcopy(self.state)
        state['state'] = 'MERGED'
        state['reviewThreads']={'nodes':state.pop('threads'),'pageInfo':{'hasNextPage':False,'endCursor':None}}
        pr = copy.deepcopy(self.pr)
        pr.update(merged=True,state='closed',merge_commit_sha=f.merged)
        values = {
            'git/ref/tags/v1.0.3': {'ref':'refs/tags/v1.0.3','object':{'sha':f.obj}},
            f'actions/workflows/{module.tag.WORKFLOW_ID}/runs?per_page=100': [{'workflow_runs':[f.tag_run]}],
            'actions/runs/41': f.tag_run,
            'pulls/123': pr, 'pulls/123/reviews?per_page=100':[self.rows],
            'git/ref/heads/main': {'object':{'sha':f.merged}},
            'git/commits/'+f.merged: {'parents':[{'sha':f.base},{'sha':f.source}],
                                    'tree':{'sha':receipt['integrated_tree']}},
            'graphql': {'data':{'repository':{'pullRequest':state}}}}
        (folder/'api.json').write_text(json.dumps(values))
        gh = folder/'gh'
        gh.write_text('#!'+sys.executable+'\n'+'''import json,os,pathlib,sys
args=sys.argv[1:]
path=next(a for a in args if a.startswith('repos/') or a=='graphql')
endpoint=path.removeprefix('repos/Sparta-fourTeam/client/')
table=json.loads((pathlib.Path(os.environ['FIXTURE_API'])/'api.json').read_text())
if endpoint not in table: raise SystemExit('Unexpected endpoint: '+endpoint)
sys.stdout.write(json.dumps(table[endpoint]))
''')
        gh.chmod(0o755)
        event, data = folder/'event.json', folder/'receipt.json'
        event.write_text(json.dumps(f.tag_event))
        data.write_text(json.dumps(receipt))
        output, summary = folder/'output', folder/'summary'
        env=dict(os.environ,**self.env,BUILD_SHA=f.merged,GITHUB_EVENT_PATH=str(event),
                 GITHUB_OUTPUT=str(output),GITHUB_STEP_SUMMARY=str(summary),FIXTURE_API=str(folder))
        env['PATH']=str(folder)+os.pathsep+env['PATH']
        command=[sys.executable,str(SCRIPT.with_name('android-release-version.py')),
                 '--automatic','--release-request',str(data)]
        before=f.policy.read_bytes()
        for extra in [[],['--expected-code','300000006']]:
            result=subprocess.run(command+extra,env=env,text=True,capture_output=True)
            self.assertEqual(result.returncode,0,result.stderr)
        self.assertEqual(output.read_text(),'version_name=1.0.3\ncode=300000006\n'*2)
        self.assertIn(f.merged,summary.read_text())
        self.assertEqual(f.policy.read_bytes(),before)
        for extra,override in [(['--expected-code','300000007'],{}),([] ,{'BUILD_SHA':f.source}),
                               ([],{'GITHUB_RUN_ATTEMPT':'2'})]:
            previous=output.read_bytes()
            result=subprocess.run(command+extra,env=dict(env,**override),text=True,capture_output=True)
            self.assertNotEqual(result.returncode,0)
            self.assertEqual(output.read_bytes(),previous)


if __name__ == '__main__':
    unittest.main()
