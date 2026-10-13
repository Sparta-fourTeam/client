---
name: "conventional-commit"
description: Use when writing a git commit message in this repository — read the enforced convention first, write a compliant message, and self-correct from hook rejections instead of bypassing them
---

# Committing in this repository (conventional-pre-commit)

This repository enforces [Conventional Commits](https://www.conventionalcommits.org)
via the `conventional-pre-commit` hook. If the hook rejects a commit, don't bypass the
format — fix only what the rejection message points at and recommit (see section 3).

## 1. This repo's agreed convention

Types in use: `feat`, `fix`, `chore`, `docs`, `refactor`, `typo`, `test`

Scope is not enforced, but fill one in when it's reasonably clear. Suggested values
by layer:

- Unity/C# repo: `ui`, `anim`, `audio`, `unity-meta`
- Domain/feature scopes (once genre is settled) are fine too, and often read better in
  a CHANGELOG than layer names — e.g. `feat(shop): ...` over `feat(ui): ...` for a
  user-facing change. Neither is required; use whichever helps a reader more.

Breaking changes: append `!` before the colon (`feat(ui)!: ...`) and explain the
break in the description; a `BREAKING CHANGE:` footer is only needed for detail
beyond what the `!` + description already conveys.

## 2. Write the message

Format: `type(scope): description`

```
feat(shop): 상점 아이템 목록 조회 UI 추가
fix(anim): 캐릭터 이동 애니메이션 전환 오류 수정
chore(unity-meta): .meta 파일 정리
```

- Description in the language the team actually writes commits in (Korean is fine —
  Conventional Commits doesn't require English).
- No period at the end of the description.
- Body (if any): one blank line after the description, then free-form paragraphs.
- Footers (if any): one blank line after the body, `Token: value` or `Token #value`,
  e.g. `Refs: #123`.

## 3. Validate before committing

There's no non-interactive "dry run" flag on the hooks themselves — running a real
commit attempt is the check. Beyond the message format, several other hooks run at
commit time (line-ending normalization, formatting, secret scanning). See
`references/pre-commit-hooks.md` for the full hook list, how each one behaves, and
commands to run them ahead of time.

Before committing new or changed public/protected members, add `/// <summary>` XML
doc comments where they're missing. This is a convention, not an enforced rule —
no hook or build step blocks the commit if it's missing (StyleCop's `SA1600` is set
to `suggestion`, so it shows as an IDE hint only). Check for it yourself as part of
review before committing.

If there are C# changes, `dotnet format` runs against the whole solution at commit
time and can be slow, so running the following ahead of time avoids any rework at
commit time (optional, not required):

```
dotnet format ProjectNova.sln --no-restore --include Assets/
```

## 4. Split unrelated changes before committing

A commit should represent one kind of change. Before writing a commit message,
check what's actually staged (`git diff --staged`, or `git status` if nothing is
staged yet). If the changes mix things that belong to different types or scopes —
e.g. a new UI feature plus an unrelated typo fix plus a `.gitignore` tweak — split
them into separate commits rather than writing one message that tries to cover
everything.

How to split:

1. Unstage everything if it's already staged in one lump: `git restore --staged .`
2. Stage one logical group at a time. Use `git add <specific files>` when whole
   files map cleanly to one change, or `git add -p` to interactively pick hunks
   within a file when a single file mixes unrelated edits.
3. Commit that group with its own conventional message.
4. Repeat for the next group until everything committed is intentional.

**Splitting criterion**: the core test is whether this commit alone can be safely
reverted. A `feat` commit's own supporting changes (e.g. a MonoBehaviour plus the
prefab plus the ScriptableObject it needs) are one logical unit — reverting them
separately would break the scene, so don't fragment changes that depend on each
other this way. Conversely, split changes that can be reverted independently of
one another.

If asked to just "commit this" and the staged/working changes clearly mix
unrelated concerns, propose the split (which files/hunks go where, with what
message) before running any `git commit` — don't silently pick one and commit
everything under it.

**Respect existing staging state.** If files are already staged (`git diff
--staged` is non-empty) when asked to commit, ask a plain scope question
*before* doing any diff analysis of your own:

> Staged: `<list the staged files>`. Commit just these, or should I also look at
> the unstaged/untracked changes?

Don't skip this by silently deciding for yourself whether the staged set "looks
coherent enough" — that judgment call belongs to the user, not to you jumping
straight into a mixed-concerns analysis or a per-file splitting proposal. Ask
this scope question first, unconditionally, whenever staged content already
exists; only after the user answers it do you move on to checking whether the
resulting scope mixes concerns (section 4 above) or is branch-unrelated (below).

**Branch-unrelated changes are a separate question from grouping.** If a change
touches a file/area that doesn't match what the current branch name or PR is
about (e.g. an unrelated `Common/` script showing up on a `feat/inventory-system`
branch), the first question is whether to commit it at all right now — not how to
group it with everything else. Ask that as its own explicit question, with an
"exclude for now" option alongside any "include" options, for example:

- Commit it now (bundled with the related changes, or as its own separate commit
  on this branch)
- Leave it uncommitted / move it to its own branch, and only commit what actually
  belongs to this branch's purpose

Don't collapse this into a "how do you want it grouped" question that assumes
inclusion — offering only bundling variants silently forecloses the "don't
commit this here at all" option the user needed to see.

## 5. Self-correct when a hook rejects

Several hooks run together, so the response depends on the kind of rejection —
auto-fix hooks (whitespace/EOF/line-ending/dotnet-format) just need a restage and
recommit, blocking hooks (merge conflicts/large files/secrets/message format) need
the underlying cause fixed by hand (see `references/pre-commit-hooks.md` for the
breakdown). For `conventional-pre-commit`, the rejection output names the problem
plainly, e.g.:

```
Conventional Commit......................................................Failed
[Bad Commit message] >> add a new feature

Your commit message does not follow Conventional Commits formatting
...
Conventional Commits start with one of the below types, followed by a colon...
```

- Fix only what the error/rejection flags (missing type, disallowed scope, wrong
  format, a detected secret, etc.) — don't rewrite unrelated parts.
- Restage (auto-fix hooks) or fix the cause (blocking hooks), then retry the commit.
- NEVER use `git commit --no-verify` to bypass any hook — fixing the problem is
  always the correct action, and bypassing defeats the whole point of enforcing
  this for the team.
