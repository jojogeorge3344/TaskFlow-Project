---
name: commit-pr-review
description: End-to-end "ship my changes" workflow in one skill - inspects the repo, confirms the user pulled latest main, asks for a new branch name, creates the branch from main carrying the current uncommitted changes, proposes a commit caption/description/file list for approval, commits, pushes, opens a GitHub Pull Request against main, then reviews the PR and posts ONE consolidated "Code Review Findings" comment on it. Use whenever the user says things like "commit my changes", "commit and push", "create a branch and raise a PR", "ship this", "push these changes and review them", "/commit-pr-review", or wants their working changes turned into a reviewed PR, even if they don't mention every step. Self-contained; does not call other skills.
license: MIT
compatibility: Requires git and gh CLI (GitHub CLI) with authenticated push and PR access to the repository.
metadata:
  author: zoccam
  version: "2.0"
---

Take the user's current working changes from "uncommitted on disk" to "open PR with a consolidated code review", in one run, without ever losing their work.

```
1 Inspect -> 2 GATE: main pulled? -> 3 GATE: branch name -> 4 Branch safety -> 5 Branch + transfer
-> 6 Prepare commit -> 7 GATE: approve caption/description/files -> 8 Commit -> 9 Push -> 10 PR
-> 11-14 Review + publish -> 15 No auto-fix -> 16 Report
```

There are three user gates (2, 3, 7). At each one, ask, then stop and wait for the reply. Everything else runs straight through unless a step hits a stop condition.

**Stop conditions, generally:** when anything fails or is ambiguous, stop at that checkpoint, leave the user's work exactly where it is, and tell them (a) what state things are in now (branch created? committed? pushed? PR open?), and (b) what needs to happen next. Never paper over a failure to keep the flow moving.

**Review timing:** the consolidated code review (steps 11–14) happens only after the PR exists. Before that, you still read the diff, check for secrets, and run tests. Those are commit-preparation and safety checks, not a review; don't produce findings or severity ratings before the PR is open.

---

## 1. Inspect the repository (read-only)

Run from the repo root (`git rev-parse --show-toplevel`). Nothing in this step changes state.

```bash
git rev-parse --abbrev-ref HEAD                   # current branch
git status --porcelain=v1 --untracked-files=all   # staged (col 1), unstaged (col 2), untracked (??)
git diff --cached --stat                          # staged
git diff --stat                                   # unstaged
git log -n 20 --pretty=format:"%h %s"             # recent history and message conventions
git stash list                                    # pre-existing stashes, so you don't confuse them with yours
gh repo view --json nameWithOwner,defaultBranchRef
```

Also check for an in-progress operation (`.git/MERGE_HEAD`, `.git/rebase-merge`, `.git/rebase-apply`, `.git/CHERRY_PICK_HEAD`). If one exists, stop and ask the user to finish or abort it.

If there are no changes at all, stop: there is nothing to commit.

Skim the diff enough to understand what the user is trying to do; you'll need it for steps 6–7.

If the repo's default branch is not `main`, use the default branch everywhere this skill says `main` and say so in the final report.

## 2. GATE: Confirm main is up to date

Branching from a stale main produces avoidable conflicts and PRs reviewed against old code. Ask:

> Before I create the branch: have you pulled the latest changes from `main`? Reply **done** / **confirm** once you have.

Stop and wait.

- If they haven't pulled, give the safe command for their situation and wait again:
  - Currently on `main`: `git pull --ff-only origin main`
  - On another branch: `git fetch origin main:main` (fast-forwards local `main` without switching branches or touching their uncommitted work)
  - They can type `! <command>` to run it here.
- After they confirm, verify it yourself rather than trusting the reply:

  ```bash
  git fetch origin main
  git rev-list --count main..origin/main   # behind: must be 0
  git rev-list --count origin/main..main   # ahead:  must be 0
  ```

  If local `main` is behind, ask them to pull again. If it's ahead (local commits on main that aren't on origin), stop and explain: those commits would silently ride along into the new branch and PR. Let the user decide how to handle them. Do not create a branch until this check passes.

## 3. GATE: Branch name

If the user already gave a branch name in this request, use it. Otherwise ask:

> What should the new branch be named?

Stop and wait. Use the name exactly as given; don't invent or "improve" one. If they ask for a suggestion, propose one matching the repo's existing style (`git branch -r` for patterns like `feature/…`, `fix/…`, `ZOC-123-…`) and wait for acceptance.

Validate:

```bash
git check-ref-format --branch "<branch>"
git show-ref --verify --quiet "refs/heads/<branch>"     # must FAIL: not present locally
git ls-remote --exit-code --heads origin "<branch>"      # must FAIL: not present on origin
```

If the name is invalid or already taken, stop, say which, and ask for another name.

## 4. Check branch safety

Before switching anything:

```bash
git rev-list --count main..HEAD
git log --oneline main..HEAD
```

If the current branch has commits that are not on `main`, those commits will **not** be carried to a new branch created from `main`; only uncommitted changes will be. List them and ask the user how to proceed (e.g. continue with only the uncommitted changes, or stop so they can handle the commits themselves). Don't silently drop them and don't cherry-pick on your own initiative.

Confirm again that there are uncommitted changes to transfer.

## 5. Create the branch and transfer changes

**If currently on `main`** (already verified up to date in step 2), just create the branch; uncommitted, staged, and untracked changes stay in the working tree:

```bash
git switch -c "<branch>"
```

**If on another branch**, move the changes via a named stash so nothing is lost:

```bash
git stash push --include-untracked -m "commit-pr-review: <branch>"
git stash list | head -1            # confirm the top entry is yours before continuing
git switch -c "<branch>" main
git stash pop
```

If `git stash pop` reports conflicts, stop. Git keeps the stash on conflict. Tell the user which files conflict, that their original changes are intact in `git stash list` under the `commit-pr-review: <branch>` entry, and wait. Don't resolve conflicts on their behalf, and never `git stash drop` or `git checkout -- <file>` to "clean up".

Confirm the result: `git rev-parse --abbrev-ref HEAD` is the new branch and `git status --porcelain=v1 --untracked-files=all` shows the expected changes.

## 6. Inspect and prepare the commit

Read the complete diff, including untracked files:

```bash
git status --porcelain=v1 --untracked-files=all
git diff                       # unstaged
git diff --cached              # staged
# for untracked files, read them directly
```

Sort every changed path into one of:

- **Include**: clearly part of the intended change.
- **Exclude**: should not be committed:
  - Secrets and credentials: `.env*`, key/cert files (`*.pem`, `*.pfx`, `id_rsa`), connection strings, tokens or passwords appearing in the diff.
  - Generated artifacts: `bin/`, `obj/`, `dist/`, `build/`, `node_modules/`, coverage output, logs.
  - IDE/OS junk: `.vs/`, `.idea/`, `.DS_Store`, `Thumbs.db`, user settings files.
  - Clearly unrelated changes, such as a stray debug edit in a file unrelated to the rest of the change.
- **Ask**: risky or ambiguous. You'll raise these at the step 7 gate.

Excluded files are simply left out of staging. They stay in the user's working tree untouched; never delete or revert them.

**Run relevant tests/checks** when the project makes them identifiable (e.g. `dotnet test`, `npm test`, `pytest`, `mvn test`, or a script named in README/CLAUDE.md), scoped to the changed area when practical. Record the exact command and the real result. If there's no identifiable test command, or tests need infrastructure that isn't available, record "Not run" with the reason. Never describe tests as passing unless you ran them and saw them pass. Failing tests aren't automatically a blocker, but they must be surfaced at the step 7 gate.

## 7. GATE: Approve caption, description, and file list

Draft the message from the actual diff.

Check the repo's convention from step 1's `git log`. Match it: Conventional Commits, ticket prefixes (`ZOC-123: …`), or whatever is established. With no clear pattern, use Conventional Commits.

**Caption (subject line)**
- `type(scope): summary`, types: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`, `build`, `ci`, `style`.
- Imperative mood ("add", not "added"), no trailing period, 72 characters or fewer.
- Describe the change's purpose, not the files touched.
- Include a ticket ID from the branch name if the repo's history does that.

**Description (body)**, wrapped at ~72 chars:
- One or two sentences on **why** (the problem or requirement).
- Bullets of the meaningful **what**: behavior changes, new endpoints/components, schema/config changes, removed code.
- Reviewer-critical notes: breaking changes (`BREAKING CHANGE: …` footer), migrations, new env vars/config keys, known limitations.
- Don't pad. A one-line fix may need a one-line body or none.

Example:

```
feat(auth): lock accounts after repeated failed logins

Repeated failed logins were never throttled, leaving accounts open
to brute-force attempts.

- Lock the account for 15 minutes after 5 consecutive failures
- Reset the failure counter on successful login
- Add LockoutMinutes and MaxFailedAttempts to auth settings
- Return 423 Locked with a retry-after hint from /api/auth/login

Config: new keys under Auth:Lockout in appsettings.json
```

Present to the user:

```
Branch: <branch>

Caption:
<caption>

Description:
<body>

Files to commit (N):
  M path/one
  A path/two

Excluded (left untouched in your working tree):
  path/.env (secret)
  bin/... (build output)

Need your decision:
  path/ambiguous (reason)

Tests: <command> -> <result>   |   Not run: <reason>

Reply "approve" to commit, or tell me what to change.
```

Stop and wait for explicit approval. If they request changes, revise and present again. Don't commit on anything short of a clear approval.

## 8. Commit

Stage exactly the approved files, nothing else. Use `git add -- <path> ...`, not `git add -A` / `git add .`. If something was already staged but isn't in the approved list, unstage it with `git restore --staged -- <path>`, which only unstages and keeps the working-tree content.

```bash
git diff --cached --name-status       # must equal the approved list exactly
git commit -F "<temp-file-with-message>"
```

- Never `--no-verify`, `--amend`, or any history rewrite.
- If a pre-commit hook fails, show its output and stop. Fixing hook failures is a code change the user should direct.

Verify:

```bash
git log -1 --format="%H %s"
git show --name-status --format= HEAD     # must match the approved list
git status --porcelain=v1 --untracked-files=all   # only the excluded/undecided files should remain
```

## 9. Push

```bash
git push -u origin "<branch>"
```

Never force-push. Verify the remote has the commit:

```bash
git ls-remote --heads origin "<branch>"   # SHA must equal HEAD
```

If the push fails (authentication, permissions, branch protection, network), stop and report the exact error text. The commit is safe locally; tell the user it can be pushed once the blocker is resolved.

## 10. Create the PR

Check for an existing PR first so you don't create a duplicate:

```bash
gh pr list --head "<branch>" --base main --state all --json number,url,state
```

If an open one exists, use it instead of creating another and say so. If only a closed/merged one exists, tell the user and ask before opening a new one.

The PR title is the commit caption. Write the body to a temp file:

```markdown
## Summary
<why this change exists, 1-3 sentences>

## Changes
- <meaningful change>
- <meaningful change>

## Notes for reviewers
<migrations, config, breaking changes, risky areas, known limitations; "None" if none>

## Testing
<exact commands run and their real results; or "Not run: <reason>">
```

```bash
gh pr create --base main --head "<branch>" --title "<caption>" --body-file "<temp-file>"
```

Verify and fetch what the review needs:

```bash
gh pr view "<branch>" --json number,title,url,state,baseRefName,headRefName,headRefOid,files,additions,deletions,changedFiles
```

Confirm `baseRefName` is `main`, `headRefName` is the new branch, and `headRefOid` equals your commit. If PR creation fails, stop and report the exact error. The branch is already pushed, so the user can open the PR manually.

## 11–13. Review the PR

Begin only after step 10 verified the PR exists. Review the PR diff against `main` as an independent reviewer would. You prepared the commit message, but the point here is to catch real problems before a human reviewer or production does. Don't soften findings because the change came through this workflow, and don't invent findings to look thorough.

In every `gh api` path, take `{owner}`, `{repo}` and `<pr-number>` from the verified PR.

### Gather the diff, sized to the PR

Check `changedFiles`/`additions`/`deletions` first.

- **Small/medium** (roughly under 1500 changed lines): `gh pr diff <pr-number>` and read all of it.
- **Large**: don't read one giant diff and skim. Get the file list and work file-by-file or group-by-group:
  ```bash
  gh pr diff <pr-number> --name-only
  gh api "repos/{owner}/{repo}/pulls/<pr-number>/files" --paginate
  ```
  Prioritise by risk: auth/security, data access and migrations, payment/money paths, public API contracts, concurrency, then business logic, then tests and config. Skip generated/vendored files (lockfiles, `*.designer.cs`, snapshots, minified bundles) except to confirm they match their source change. If subagents are available, fan out one per file group and merge their findings; otherwise review groups sequentially.
- Whatever the size, track which files were reviewed in depth and which were skimmed or skipped, so the report doesn't imply coverage that didn't happen.

### Read surrounding code

Judge each change in context: callers, tests, sibling implementations. You're on the PR branch locally, so read files directly. Read enough to confirm or rule out each potential finding.

### Check against specs/requirements (when present)

Look in this order and stop when you have enough:
- The PR description and any linked issues/ticket IDs
- Repo docs: `openspec/` (changes, specs), `docs/`, `specs/`, ADRs, LLDs, `README`, `CLAUDE.md`
- Acceptance criteria in the linked ticket

Compare what the PR does with what was specified: missing requirements, behavior contradicting a spec, unmet acceptance criteria, scope creep. If no specs exist, note it in one line; don't invent requirements.

### What to check, as relevant to the change

- Correctness and logic bugs, edge cases, regression risk to existing callers
- Security: authn/authz, injection, secrets, input validation, sensitive data exposure
- Error handling and failure paths
- Performance: N+1 queries, unbounded loops, blocking calls, memory
- Data integrity: transactions, migrations, concurrency, idempotency
- API behavior and contract changes, backward compatibility
- Consistency with existing architecture, coding patterns, and similar implementations
- Maintainability
- Tests and coverage for the changed behavior

### Filter, consolidate, classify

- Report only actionable, evidence-based issues, each verified against the actual code. A concern you couldn't confirm is not a confirmed bug. Either verify it or leave it out (or mention it as a verification limitation).
- Merge duplicate or related issues into one finding; never report the same issue twice.
- Skip style-only nitpicks and issues already handled elsewhere.
- Classify by real impact, don't inflate:
  - 🔴 Critical: must be fixed before merge.
  - 🟠 High: significant functional, security, or data issue.
  - 🟡 Medium: important correctness, maintainability, or edge-case issue.
  - 🔵 Low: minor issue worth addressing.

Every finding includes **Location** (`path/to/file.ext:line`), **Issue**, **Impact**, **Suggested Fix**. Severity is given by the section it sits under.

### Comment format

```markdown
## Code Review Findings

I reviewed the complete PR and identified the following actionable issues.

### 🔴 Critical

1. **[Issue title]**
   - **Location:** `path/to/file.cs:123`
   - **Issue:** ...
   - **Impact:** ...
   - **Suggested Fix:** ...

### 🟠 High

1. **[Issue title]**
   - **Location:** `path/to/file.cs:456`
   - **Issue:** ...
   - **Impact:** ...
   - **Suggested Fix:** ...

### 🟡 Medium

1. **[Issue title]**
   - **Location:** `path/to/file.cs:789`
   - **Issue:** ...
   - **Impact:** ...
   - **Suggested Fix:** ...

### 🔵 Low

1. **[Issue title]**
   - **Location:** `path/to/file.cs:100`
   - **Issue:** ...
   - **Impact:** ...
   - **Suggested Fix:** ...

### Summary

| Severity | Count |
|---|---:|
| 🔴 Critical | X |
| 🟠 High | X |
| 🟡 Medium | X |
| 🔵 Low | X |
| **Total** | **X** |
```

Omit any severity section with no findings and drop its row from the Summary table. Keep the heading exactly `## Code Review Findings`; the `code-review-resolved` workflow finds the comment by that prefix. For large PRs, add a short `### Review Coverage` note listing skimmed or skipped files.

If there are no actionable issues:

```markdown
## Code Review Findings

✅ No actionable issues were identified during the review.

The implementation was reviewed against the PR changes, surrounding code, relevant project requirements, error handling, security, performance, and tests.

**Verification limitations:** <e.g. tests not run because ..., files skimmed, no specs found; or "None">
```

## 14. Publish the review on the same PR

Post exactly ONE comment, on the PR from step 10. Do not post per-issue comments, and do not approve, request changes on, label, or merge the PR.

Write the body to a temp file, then:

```bash
gh pr comment <pr-number> --body-file "<temp-file>"
```

`gh pr comment` prints the comment URL on success. Verify it really landed on the right PR:

```bash
gh api "repos/{owner}/{repo}/issues/<pr-number>/comments" --paginate \
  --jq '[.[] | select(.body | startswith("## Code Review Findings"))] | last | .html_url'
```

If posting fails (permissions, authentication, API limits), report the exact error and print the complete consolidated review in the terminal so nothing is lost. Never say the comment was posted unless this verification returned its URL.

## 15. Do not fix findings

The review phase ends with the published comment. Don't modify code, create additional commits, push fixes, or merge the PR unless the user explicitly asks afterwards.

## 16. Final report

Summarize, stating only what was verified:

- **Branch:** name, created from `main` (verified up to date with `origin/main`)
- **Commit:** caption and full hash
- **Push:** status (verified against remote)
- **PR:** number and URL
- **Findings:** count per severity and total
- **Review comment:** published (with URL) / not published (exact blocker, review shown above)
- **Tests and checks:** each command actually run with its result, or "Not run: <reason>"
- **Failed or skipped steps** and why. Also list leftovers the user should know about: excluded files still in their working tree, an un-popped stash, undecided files.

## Safety rules

- Never lose, overwrite, or silently discard user changes. No `git reset --hard`, `git checkout -- <file>`, `git clean`, `git stash drop`, or deleting files.
- Never stage or commit unrelated files or secrets; stage explicit paths only.
- Never force-push, bypass hooks (`--no-verify`), amend, or rewrite shared history. Never commit to or push `main`.
- Never create a branch until the step 2 main-update check passes.
- Never commit until the user approves the caption, description, and file list.
- Never run the consolidated code review before the PR exists. Diff inspection, secret checks, and tests before that point are preparation, not review.
- Never claim tests, push, PR creation, or comment publication succeeded unless verified.
- On failure, stop at that checkpoint, preserve the user's work, and explain what's next.
- Respect repository conventions, branch protections, and existing GitHub permissions.
- Don't fix review findings or merge the PR.
- This skill is standalone: don't invoke or depend on any other skill.
