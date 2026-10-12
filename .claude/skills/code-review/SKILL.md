---
name: code-review
description: Reviews a GitHub Pull Request and posts ONE consolidated "Code Review Findings" comment (optionally with inline line comments). Use whenever the user asks for a code review, PR review, /code-review, "review this PR", "check this PR before merge", "look over my changes", "find issues in this pull request", or shares a GitHub PR link and wants feedback, even if they don't say "code review" explicitly. Requires BOTH the PR link and the branch name; asks for whichever is missing.
license: MIT
compatibility: Requires gh CLI (GitHub CLI) with authenticated access to the repository.
metadata:
  author: zoccam
  version: "2.0"
---

Review a GitHub Pull Request end-to-end and publish a single consolidated review comment.

## Required inputs (gate: do this first)

Two inputs are mandatory: the **GitHub PR link** and the **branch name**. Both are needed so the work targets exactly the PR and code the user means; a wrong guess means posting a comment on the wrong PR, which is public and hard to undo.

Look only at what the user explicitly typed in this request. Do not infer from the current git branch, `gh pr view` auto-detection, recent PRs, earlier conversation turns, or repo state, and do not pick a value on the user's behalf.

- Both provided: continue to Step 1.
- PR link missing, branch present: ask "Please provide the GitHub PR link. I need it before starting the code review."
- Branch name missing, link present: ask "Please provide the branch name. I need it before starting the code review."
- Both missing: ask exactly:

  > Please provide both:
  >
  > 1. GitHub PR link
  > 2. Branch name
  >
  > I need both before starting the code review.

Then stop and wait. Do not run any `gh`/`git` command, read any file, or begin analysis until both values are in hand. A bare PR number is not a link; ask for the full link.

After both are provided, confirm they belong together:

```bash
gh pr view "<pr-link>" --json number,title,url,baseRefName,headRefName,state
```

If `headRefName` differs from the provided branch name, stop and tell the user about the mismatch (the PR says `X`, you gave `Y`), then ask which is correct. Never silently pick one.

**Optional input**: `inline` (e.g. "with inline comments"). See Step 8. Off by default.

**Steps**

In every `gh api` path below, take `{owner}`, `{repo}` and `<pr-number>` from the PR link itself, and pass `--repo {owner}/{repo}` (or use the explicit path) so commands hit that repo even when run from another directory.

1. **Resolve the target PR** (after the gate above passes)

   ```bash
   gh pr view "<pr-link>" --json number,title,url,baseRefName,headRefName,headRefOid,body,files,additions,deletions,changedFiles
   ```

   If the PR is closed or merged, tell the user and ask whether to continue.

2. **Gather the diff, sized to the PR**

   Check `changedFiles`/`additions`/`deletions` first.

   - **Small/medium** (roughly under 1500 changed lines): `gh pr diff "<pr-link>"` and read all of it.
   - **Large**: do not read one giant diff and skim. Get the file list, then work file-by-file or group-by-group:
     ```bash
     gh pr diff "<pr-link>" --name-only
     gh api "repos/{owner}/{repo}/pulls/<pr-number>/files" --paginate
     ```
     Prioritise by risk: auth/security, data access and migrations, payment/money paths, public API contracts, concurrency, then business logic, then tests and config. Skip generated/vendored files (lockfiles, `*.designer.cs`, snapshots, minified bundles) except to confirm they match their source change. If subagents are available, fan out one per file group and merge their findings; otherwise review groups sequentially.
   - Whatever the size, state in the final report which files were reviewed in depth and which were skimmed or skipped, so nobody assumes coverage that didn't happen.

3. **Read surrounding code on the PR branch**

   Judge each change in context: callers, tests, sibling implementations. Read the code as it exists on the provided branch. If the local checkout is on that branch, read locally; otherwise `git fetch origin <branch>` then `git show origin/<branch>:<path>`, or `gh api "repos/{owner}/{repo}/contents/<path>?ref=<branch>"`. Do not check out or switch branches.

4. **Check against the project's specs/requirements (when present)**

   Look for requirement sources in this order and stop when you have enough:
   - The PR description and linked issues (`gh pr view "<pr-link>" --json body,closingIssuesReferences`)
   - Repo docs: `openspec/` (changes, specs), `docs/`, `specs/`, ADRs, LLDs, `README`, `CLAUDE.md`
   - Acceptance criteria in the linked ticket

   Compare what the PR does with what was specified: missing requirements, behavior that contradicts a spec, unmet acceptance criteria, scope creep into unrelated areas. If no specs exist, say so in one line in your report and move on; don't invent requirements.

5. **Check the implementation against**

   - Existing project architecture and coding patterns
   - Related and similar existing implementations
   - Error handling and failure paths
   - Security (authz/authn, injection, secrets, input validation, sensitive data exposure)
   - Performance (N+1 queries, unbounded loops, blocking calls, memory)
   - Data integrity (transactions, migrations, concurrency, idempotency)
   - API behavior and contract changes, backward compatibility
   - Tests and coverage for the changed behavior

6. **Filter and verify findings**

   - Keep only genuine, actionable issues; verify each against the actual code on the branch before reporting.
   - Deduplicate related findings; never report the same issue twice.

7. **Classify severity** based on real impact; don't inflate.

   - 🔴 Critical: must be fixed before merge.
   - 🟠 High: significant functional, security, or data issue.
   - 🟡 Medium: important correctness, maintainability, or edge-case issue.
   - 🔵 Low: minor issue worth addressing.

8. **Post**

   **Consolidated comment (always).** Write the body to a temp file (avoids shell-escaping problems), then:

   ```bash
   gh pr comment "<pr-link>" --body-file "<temp-file>"
   ```

   **Inline line comments (only if the user asked for `inline`).** After the consolidated comment, attach line comments for findings that sit on a line of the PR diff. Submit them as ONE review with event `COMMENT` so they arrive as a single notification:

   ```bash
   gh api "repos/{owner}/{repo}/pulls/<pr-number>/reviews" --method POST --input review.json
   ```

   `review.json`: `{"commit_id": "<headRefOid>", "event": "COMMENT", "body": "Inline comments for the Code Review Findings above.", "comments": [{"path": "src/A.cs", "line": 123, "side": "RIGHT", "body": "**🟠 High: title**\nissue and suggested fix"}]}`. Lines must be inside the diff; a finding that isn't on a diff line stays in the consolidated comment only. Keep each inline body short and point to the consolidated comment for detail.

   If there are no actionable issues, post the "No Issues Found" variant and no inline comments.

**Finding Format**

Every finding must include:

- **Location:** `path/to/file.ext:line`
- **Issue:** What is wrong, stated plainly.
- **Impact:** Why it matters.
- **Suggested Fix:** Concise technical direction.

**Comment Format**

Post exactly ONE consolidated comment using:

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

Omit any severity section with no findings and remove its row from the Summary table. The heading must stay exactly `## Code Review Findings`; `code-review-resolved` finds it by that prefix.

**If No Issues Are Found**

```markdown
## Code Review Findings

✅ No actionable issues were identified during the review.

The implementation was reviewed against the PR changes, surrounding code, relevant project requirements, error handling, security, performance, and tests.
```

**Output After Posting**

Report: PR URL, branch, total findings with per-severity breakdown, whether inline comments were posted, which specs were checked (or that none were found), and review coverage for large PRs.

**Guardrails**

- Never start without both the PR link and branch name from the user; never guess or auto-detect either.
- ONE consolidated comment per run. Inline comments only when explicitly requested, and only as a single review.
- Never create duplicate findings.
- Do not modify source code; no commits, branches, pushes, or checkouts.
- Do not approve, request changes on, label, or merge the PR.
- Do not report purely stylistic preferences, speculative problems without evidence in the code, or issues already handled elsewhere.
- Focus on correctness, security, reliability, maintainability, performance, and requirements compliance.
