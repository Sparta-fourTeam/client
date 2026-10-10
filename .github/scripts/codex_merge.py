#!/usr/bin/env python3
"""Fail-closed, model-free Codex merge gate. Never checks out PR code."""

import datetime as dt
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

REPOSITORY = "Sparta-fourTeam/client"
REPOSITORY_ID = 1388071718
AUTHOR_ID = 79277772
CODEX_ID = 199175422
CODEX_LOGIN = "chatgpt-codex-connector[bot]"
BASE = "dev"
MARKER = "<!-- codex-pull-request-review-summary -->"
TEST_WORKFLOW = ".github/workflows/codex-merge-policy-tests.yml"
SHA = re.compile(r"[0-9a-f]{40}")


class Hold(Exception):
    """Missing, incomplete or negative evidence: leave the PR unchanged."""


class ApiError(Exception):
    pass


def require(condition, message):
    if not condition:
        raise Hold(message)


def timestamp(value):
    try:
        parsed = dt.datetime.fromisoformat(value.replace("Z", "+00:00"))
        require(parsed.tzinfo is not None, "timestamp has no timezone")
        return parsed
    except (ValueError, AttributeError, TypeError) as error:
        raise Hold("missing or invalid timestamp") from error


def is_codex(user):
    return (user or {}).get("id") == CODEX_ID and user.get("login") == CODEX_LOGIN and user.get("type") == "Bot"


class GitHub:
    def __init__(self, token):
        require(bool(token), "GITHUB_TOKEN is missing")
        self.token = token

    def request(self, path, method="GET", body=None):
        # Only constant-host, relative API paths; never forward the token to URLs
        # found in comments, API responses, or pagination links.
        require(path.startswith("/") and not path.startswith("//"), "invalid API path")
        data = None if body is None else json.dumps(body).encode()
        req = urllib.request.Request(
            "https://api.github.com" + path, data=data, method=method,
            headers={"Authorization": "Bearer " + self.token,
                     "Accept": "application/vnd.github+json",
                     "X-GitHub-Api-Version": "2022-11-28",
                     "Content-Type": "application/json"},
        )
        # Redirects are errors, including for writes: do not retry an uncertain merge.
        class NoRedirect(urllib.request.HTTPRedirectHandler):
            def redirect_request(self, *args, **kwargs):
                return None
        try:
            with urllib.request.build_opener(NoRedirect).open(req, timeout=30) as response:
                return json.load(response)
        except (urllib.error.URLError, ValueError) as error:
            raise ApiError(f"GitHub {method} request failed: {path.split('?')[0]}") from error

    def get(self, path):
        return self.request(path)

    def pages(self, path, key=None):
        output = []
        for page in range(1, 101):
            sep = "&" if "?" in path else "?"
            result = self.get(f"{path}{sep}per_page=100&page={page}")
            values = result.get(key) if key else result
            require(isinstance(values, list), "invalid paginated API response")
            output.extend(values)
            if len(values) < 100:
                if key and "total_count" in result:
                    require(len(output) == result["total_count"], "pagination count changed or incomplete")
                return output
        raise Hold("pagination limit reached; manual review needed")

    def comment_provenance(self, node_id):
        require(isinstance(node_id, str) and node_id, "missing summary node ID")
        query = """query($id: ID!) {
          node(id: $id) { ... on IssueComment {
            id body updatedAt lastEditedAt
            author { __typename ... on Bot { databaseId login } }
            editor { __typename ... on Bot { databaseId login } }
          } }
        }"""
        result = self.request("/graphql", "POST", {"query": query, "variables": {"id": node_id}})
        require(not result.get("errors"), "summary provenance query failed")
        comment = result.get("data", {}).get("node")
        require(isinstance(comment, dict), "summary provenance missing")
        return comment

    def threads(self, number):
        query = """query($cursor: String, $number: Int!) {
          repository(owner: "Sparta-fourTeam", name: "client") {
            pullRequest(number: $number) {
              headRefOid baseRefOid mergeable mergeStateStatus reviewDecision
              reviewThreads(first: 100, after: $cursor) {
                nodes { id isResolved }
                pageInfo { hasNextPage endCursor }
              }
            }
          }
        }"""
        cursor = None
        nodes = []
        snapshot = None
        for _ in range(100):
            result = self.request("/graphql", "POST", {"query": query, "variables": {"number": number, "cursor": cursor}})
            require(not result.get("errors"), "GraphQL returned errors")
            pr = result.get("data", {}).get("repository", {}).get("pullRequest")
            require(isinstance(pr, dict), "missing GraphQL PR")
            state = {key: pr.get(key) for key in ("headRefOid", "baseRefOid", "mergeable", "mergeStateStatus", "reviewDecision")}
            if snapshot is None:
                snapshot = state
            require(state == snapshot, "PR changed during thread pagination")
            connection = pr.get("reviewThreads", {})
            require(isinstance(connection.get("nodes"), list), "missing review threads")
            nodes.extend(connection["nodes"])
            info = connection.get("pageInfo", {})
            require(isinstance(info.get("hasNextPage"), bool), "missing thread pagination")
            if not info["hasNextPage"]:
                return snapshot, nodes
            require(info.get("endCursor") and info["endCursor"] != cursor, "invalid thread cursor")
            cursor = info["endCursor"]
        raise Hold("review thread pagination limit reached")


def summary_evidence(comments, head, api, prefix):
    # Exactly one authenticated summary. An edited/in-progress/unknown format
    # invalidates earlier evidence; never search backwards for an old green row.
    summaries = [comment for comment in comments if is_codex(comment.get("user")) and MARKER in comment.get("body", "")]
    require(len(summaries) == 1, "missing or ambiguous Codex summary")
    comment = summaries[0]
    provenance = api.comment_provenance(comment.get("node_id"))
    author = provenance.get("author") or {}
    editor = provenance.get("editor") or {}
    require(author.get("__typename") == "Bot" and author.get("databaseId") == CODEX_ID and author.get("login") == CODEX_LOGIN, "summary author provenance mismatch")
    require(provenance.get("lastEditedAt") is None or (editor.get("__typename") == "Bot" and editor.get("databaseId") == CODEX_ID and editor.get("login") == CODEX_LOGIN), "Codex summary was edited by another actor")
    require(provenance.get("id") == comment.get("node_id") and provenance.get("body") == comment.get("body") and timestamp(provenance.get("updatedAt")) == timestamp(comment.get("updated_at")), "summary changed during validation")
    body = comment["body"]
    require(body.startswith(MARKER + "\n"), "unknown Codex summary format")
    rows = [line.strip() for line in body.splitlines() if line.strip().startswith("|")]
    require(len(rows) >= 3 and rows[0] == "| Review | Status | Commit | Review trigger |" and rows[1] == "| --- | --- | --- | --- |", "unknown Codex summary table")
    completed = []
    has_code_review = False
    for row in rows[2:]:
        cells = [cell.strip() for cell in row.strip("|").split("|")]
        require(len(cells) == 4, "unknown Codex summary row")
        require(cells[0] in ("📝 **Code Review**", "🔒 **Security Review**"), "unknown Codex review kind")
        has_code_review |= cells[0] == "📝 **Code Review**"
        match = re.fullmatch(r'✅ \*\*Completed\*\* <relative-time datetime="([^"<>]+)">[^<>]+</relative-time>', cells[1])
        require(match is not None, "Codex review is not completed")
        commit = re.fullmatch(r"`([0-9a-f]{7,40})`", cells[2])
        require(commit is not None, "unknown reviewed commit")
        # Let GitHub resolve abbreviations; ambiguity/error holds the PR. A
        # matching prefix alone is never sufficient evidence of the reviewed SHA.
        resolved = api.get(f"{prefix}/commits/{commit[1]}")
        require(resolved.get("sha") == head, "Codex reviewed a different commit")
        completed.append(timestamp(match[1]))
    require(has_code_review and completed, "missing completed code review")
    finished = max(completed)
    require(timestamp(comment.get("updated_at")) + dt.timedelta(seconds=1) >= finished, "summary timestamp mismatch")
    for other in comments:
        if is_codex(other.get("user")) and other.get("id") != comment.get("id"):
            require(timestamp(other.get("updated_at")) <= finished, "newer Codex comment needs review")
    return finished, comment["id"], comment["updated_at"], comment["body"]


def validate_reviews(reviews, finished):
    latest = {}
    for review in sorted(reviews, key=lambda row: row.get("id", 0)):
        state = review.get("state")
        require(state in ("APPROVED", "CHANGES_REQUESTED", "COMMENTED", "DISMISSED", "PENDING"), "unknown review state")
        if is_codex(review.get("user")) and state != "PENDING":
            require(timestamp(review.get("submitted_at")) <= finished, "newer Codex review needs a new completed summary")
        # COMMENTED must not clear a prior request for changes.
        if state in ("APPROVED", "CHANGES_REQUESTED"):
            user_id = (review.get("user") or {}).get("id")
            require(isinstance(user_id, int), "review author missing")
            latest[user_id] = state
    require("CHANGES_REQUESTED" not in latest.values(), "unresolved request for changes")


def validate_checks(api, prefix, pr):
    # Inspect both head and GitHub's current test-merge commit. Required checks
    # may report on either; all observed checks must be green, a stricter gate.
    refs = {pr["head"]["sha"], pr.get("merge_commit_sha")}
    require(all(isinstance(ref, str) and SHA.fullmatch(ref) for ref in refs), "test-merge SHA is unavailable")
    for ref in sorted(refs):
        checks = api.pages(f"{prefix}/commits/{ref}/check-runs?filter=latest", "check_runs")
        for check in checks:
            require(check.get("head_sha") == ref, "check belongs to another commit")
            require(check.get("status") == "completed" and check.get("conclusion") in ("success", "neutral", "skipped"), "CI check is pending or failing")
        statuses = api.pages(f"{prefix}/commits/{ref}/statuses")
        latest = {}
        for status in statuses:  # API returns newest first.
            require(isinstance(status.get("context"), str), "status context missing")
            latest.setdefault(status["context"], status)
        require(all(item.get("state") == "success" for item in latest.values()), "commit status is pending or failing")
    # A missing check is not a pass. Always require our read-only test workflow
    # for this exact PR head, independent of whether branch CI is configured.
    workflow = urllib.parse.quote(TEST_WORKFLOW.rsplit("/", 1)[-1], safe="")
    runs = api.pages(f"{prefix}/actions/workflows/{workflow}/runs?event=pull_request&head_sha={pr['head']['sha']}", "workflow_runs")
    relevant = [run for run in runs if run.get("head_sha") == pr["head"]["sha"] and run.get("head_repository", {}).get("id") == REPOSITORY_ID and any(item.get("number") == pr["number"] for item in run.get("pull_requests", []))]
    require(bool(relevant), "policy-test workflow has not run for this PR head")
    latest_run = max(relevant, key=lambda run: (run.get("run_number", 0), run.get("run_attempt", 0)))
    associated = [item for item in latest_run.get("pull_requests", []) if item.get("number") == pr["number"]]
    require(len(associated) == 1 and associated[0].get("head", {}).get("sha") == pr["head"]["sha"] and associated[0].get("base", {}).get("sha") == pr["base"]["sha"], "policy tests used a different head or base")
    require(latest_run.get("path") == TEST_WORKFLOW and latest_run.get("event") == "pull_request", "unexpected policy-test workflow")
    require(latest_run.get("status") == "completed" and latest_run.get("conclusion") == "success", "policy-test workflow is not successful")


def inspect(api, number):
    prefix = f"/repos/{REPOSITORY}"
    pr = api.get(f"{prefix}/pulls/{number}")
    require(pr.get("number") == number and pr.get("state") == "open" and pr.get("draft") is False and pr.get("merged") is False, "PR is closed, merged or draft")
    require(pr.get("user", {}).get("id") == AUTHOR_ID and pr["user"].get("type") == "User", "PR author is not Eunseok")
    require(pr.get("base", {}).get("ref") == BASE and pr["base"].get("repo", {}).get("id") == REPOSITORY_ID, "unexpected base repository or branch")
    require(pr.get("head", {}).get("repo", {}).get("id") == REPOSITORY_ID, "fork PRs are excluded")
    files = api.pages(f"{prefix}/pulls/{number}/files")
    require(len(files) == pr.get("changed_files"), "changed-file listing is incomplete")
    # Protect whole executable directories, including newly added Python modules
    # that could otherwise shadow stdlib imports on a subsequent privileged run.
    protected_prefixes = (".github/scripts/", ".github/workflows/")
    require(not any(str(file.get(key, "")).startswith(protected_prefixes) for file in files for key in ("filename", "previous_filename")), "workflow or merge-policy changes require manual merge")
    head = pr["head"].get("sha")
    require(isinstance(head, str) and SHA.fullmatch(head), "invalid head SHA")
    require(pr.get("mergeable") is True and pr.get("mergeable_state") == "clean", "mergeability is not clean")
    graph, threads = api.threads(number)
    require(graph["headRefOid"] == head and graph["baseRefOid"] == pr["base"]["sha"], "PR refs changed")
    require(graph["mergeable"] == "MERGEABLE" and graph["mergeStateStatus"] == "CLEAN", "GitHub merge gate is not clean")
    require(graph["reviewDecision"] in (None, "APPROVED"), "required reviews are not approved")
    require(all(thread.get("isResolved") is True for thread in threads), "unresolved review thread")
    # Required check names are ultimately enforced by GitHub at merge time.
    # Branch metadata exposes classic requirements without admin privileges;
    # active ruleset requirements are readable with Metadata permission.
    branch = api.get(f"{prefix}/branches/{BASE}")
    require(branch.get("commit", {}).get("sha") == pr["base"]["sha"], "base branch changed")
    require(isinstance(branch.get("protected"), bool), "branch protection metadata missing")
    rules = api.pages(f"{prefix}/rules/branches/{BASE}")
    require(all(isinstance(rule.get("type"), str) for rule in rules), "ruleset metadata incomplete")
    require(not any(rule["type"] == "merge_queue" for rule in rules), "merge queue requires a different merge workflow")
    comments = api.pages(f"{prefix}/issues/{number}/comments")
    evidence = summary_evidence(comments, head, api, prefix)
    finished = evidence[0]
    reactions = api.pages(f"{prefix}/issues/{number}/reactions")
    require(not any(is_codex(item.get("user")) and item.get("content") == "eyes" for item in reactions), "Codex still has a running-review reaction")
    thumbs = [item for item in reactions if is_codex(item.get("user")) and item.get("content") == "+1"]
    require(len(thumbs) == 1 and timestamp(thumbs[0].get("created_at")) >= finished, "fresh Codex thumbs-up is missing")
    validate_reviews(api.pages(f"{prefix}/pulls/{number}/reviews"), finished)
    validate_checks(api, prefix, pr)
    return {"head": head, "base": pr["base"]["sha"], "merge": pr["merge_commit_sha"], "evidence": evidence, "reaction": thumbs[0]["id"]}


def merge_one(api, number):
    first = inspect(api, number)
    # Fetch every gate again before the sole write. GitHub atomically checks the
    # expected head and its own protection rules at merge time. A changed head
    # yields 409, never a retry with the new SHA.
    second = inspect(api, number)
    require(first == second, "approval evidence or PR refs changed during validation")
    result = api.request(f"/repos/{REPOSITORY}/pulls/{number}/merge", "PUT", {
        "sha": first["head"], "merge_method": "squash",
        "commit_title": f"chore: merge Codex-reviewed PR #{number}",
        "commit_message": f"Codex review and CI verified for {first['head']}.",
    })
    require(result.get("merged") is True and SHA.fullmatch(result.get("sha", "")), "merge was not confirmed; inspect GitHub before retrying")
    return result["sha"]


def accepts_event(name, event):
    if event.get("repository", {}).get("id") != REPOSITORY_ID or event["repository"].get("full_name") != REPOSITORY:
        return False
    if name == "issue_comment":
        comment = event.get("comment", {})
        return bool(event.get("issue", {}).get("pull_request")) and event.get("action") in ("created", "edited") and (
            is_codex(comment.get("user")) or
            (comment.get("user", {}).get("id") == AUTHOR_ID and comment.get("body", "").strip() == "/codex-merge"))
    if name == "pull_request_target":
        return event.get("action") in ("opened", "synchronize", "reopened", "ready_for_review") and event.get("pull_request", {}).get("base", {}).get("ref") == BASE
    if name == "workflow_run":
        run = event.get("workflow_run", {})
        return event.get("action") == "completed" and run.get("name") in ("Codex merge policy tests", "Build Dev APK") and run.get("head_repository", {}).get("id") == REPOSITORY_ID
    if name == "check_run":
        return event.get("action") == "completed"
    if name == "status":
        return event.get("state") in ("success", "failure", "error")
    return False


def main():
    event = json.loads(Path(os.environ["GITHUB_EVENT_PATH"]).read_text())
    if not accepts_event(os.environ["GITHUB_EVENT_NAME"], event):
        print("Event is outside the authorized merge scope.")
        return
    require(os.environ.get("GITHUB_REPOSITORY") == REPOSITORY, "unexpected workflow repository")
    api = GitHub(os.environ.get("GITHUB_TOKEN"))
    repo = api.get(f"/repos/{REPOSITORY}")
    require(repo.get("id") == REPOSITORY_ID and repo.get("default_branch") == BASE and repo.get("allow_squash_merge") is True, "repository configuration changed")
    # Scanning all eligible open PRs on each event makes repository-wide
    # concurrency safe when GitHub replaces a pending run with a newer event.
    pulls = api.pages(f"/repos/{REPOSITORY}/pulls?state=open&base={BASE}")
    for pr in pulls:
        if pr.get("user", {}).get("id") != AUTHOR_ID or pr.get("draft") is not False:
            continue
        number = pr["number"]
        for attempt in range(3):
            try:
                merged = merge_one(api, number)
                print(f"Merged PR #{number}: {merged}")
                break
            except Hold as error:
                print(f"PR #{number}: held ({error}).")
                # Reaction creation can follow the summary edit by a few
                # seconds. Only that eventual-consistency gap is retried.
                if str(error) != "fresh Codex thumbs-up is missing" or attempt == 2:
                    break
                time.sleep(10)
            except ApiError:
                # A write might have reached GitHub. Never retry it or another
                # PR after any API failure in this run.
                raise


if __name__ == "__main__":
    try:
        main()
    except (Hold, ApiError) as error:
        print(f"Stopped safely: {error}", file=sys.stderr)
        sys.exit(1)
