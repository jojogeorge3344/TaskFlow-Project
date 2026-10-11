---
name: code-review-resolved
description: Verifies fixes on a GitHub Pull Request and posts ONE consolidated "Resolved Items" comment. Use after a /code-review run, when the user asks to re-review a PR, verify fixes, check whether review comments were addressed, or run /code-review-resolved. Requires BOTH the PR link and the branch name; asks for whichever is missing.
license: MIT
compatibility: Requires gh CLI (GitHub CLI) with authenticated access to the repository.
metadata:
  author: zoccam
  version: "2.0"
---

Verify previously reported code-review findings on a GitHub Pull Request and publish a single consolidated "Resolved Items" comment.

## Required inputs (gate: do this first)

Two inputs are mandatory: the **GitHub PR link** and the **branch name**. Both are needed so the work targets exactly the PR and code the user means; a wrong guess means posting a comment on the wrong PR, which is public and hard to undo.

Look only at what the user explicitly typed in this request. Do not infer from the current git branch, `gh pr view` auto-detection, recent PRs, earlier conversation turns, or repo state, and do not pick a value on the user's behalf.

- Both provided: continue to Step 1.
- PR link missing, branch present: ask "Please provide the GitHub PR link. I need it before starting the verification."
- Branch name missing, link present: ask "Please provide the branch name. I need it before starting the verification."
- Both missing: ask exactly:

  > Please provide both:
  >
  > 1. GitHub PR link
  > 2. Branch name
  >
  > I need both before starting the verification.

Then stop and wait. Do not run any `gh`/`git` command, read any file, or begin analysis until both values are in hand. A bare PR number is not a link; ask for the full link.

After both are provided, confirm they belong together:

```bash
gh pr view "<pr-link>" --json number,title,url,baseRefName,headRefName,state
```

If `headRefName` differs from the provided branch name, stop and tell the user about the mismatch (the PR says `X`, you gave `Y`), then ask which is correct. Never silently pick one.

**Steps**

1. **Resolve the target PR** (after the gate above passes)

   ```bash
   gh pr view "<pr-link>" --json number,title,url,baseRefName,headRefName,headRefOid
   ```

2. **Fetch the current diff and files**

   ```bash
   gh pr diff "<pr-link>"
   gh pr view "<pr-link>" --json commits,files
   ```

3. **Collect the previous code-review findings**

   ```bash
   gh api "repos/{owner}/{repo}/issues/<pr-number>/comments" --paginate
   ```

   Take `{owner}`, `{repo}` and `<pr-number>` from the PR link itself, and pass `--repo {owner}/{repo}` (or use the explicit path) so commands hit that repo even when run from another directory.
   Filter for the comments whose body starts with `## Code Review Findings` (posted by `/code-review`). Parse out each finding: title, location, issue, and suggested fix.

   If no prior `## Code Review Findings` comment exists, stop and tell the user — there is nothing to verify.

4. **Verify each previously reported issue against the current code**

   For every finding from the previous review:
   - Locate the file/area named in the finding and read the current implementation on the provided branch (local checkout if it is on that branch, else `git fetch origin <branch>` + `git show origin/<branch>:<path>`; never switch branches).
   - Check the current diff to see how it changed.
   - Decide: **Resolved** or **Still Open**.
   - Never rely on the developer's claim alone — the code is the source of truth.
   - Do not mark an issue resolved without evidence.

5. **Re-verify the severity assumption**

   A fix is only resolved if it addresses the root cause, not just the symptom. If a fix introduces a new problem in the same area, keep the original finding open and note why.

6. **Write ONE consolidated comment**

   Write the body to a temp file, then post it:

   ```bash
   gh pr comment "<pr-link>" --body-file "<temp-file>"
   ```

   Include only findings from the previous code review — do not add unrelated observations. Omit the `### ⚠️ Still Open` section entirely if everything is resolved.

**Comment Format**

Post exactly ONE comment:

```markdown
## Code Review — Resolved Items

The following issues from the previous review have been verified as resolved.

### ✅ Resolved

1. **[Issue title]**
   - **Location:** `path/to/file.cs:123`
   - **Resolution:** [Brief explanation of what changed and why it resolves the issue.]

2. **[Issue title]**
   - **Location:** `path/to/file.cs:456`
   - **Resolution:** [Brief explanation.]

### ⚠️ Still Open

1. **[Issue title]**
   - **Location:** `path/to/file.cs:789`
   - **Reason:** [Why the issue is still unresolved.]

### Summary

| Status | Count |
|---|---:|
| ✅ Resolved | X |
| ⚠️ Still Open | X |
```

If everything is resolved, append:

> **All previously reported review findings have been verified as resolved.**

**Output After Posting**

Report back to the user: PR URL, branch, counts of resolved vs still open, and a short list of anything that remains open.

**Guardrails**

- Never start without both the PR link and branch name from the user; never guess or auto-detect either.
- ONE consolidated comment only — never one comment per resolved issue.
- Never duplicate the same finding.
- Only include findings from the previous `/code-review` comment.
- Verify the current code before marking any issue resolved.
- Do not treat unrelated changes as resolved review findings.
- Do not report new issues here — this skill only tracks prior findings. New problems go through `/code-review`.
- Keep explanations concise and technically accurate.
- Do not modify the PR code.
- Do not create commits, branches, or pushes.
- Do not approve, request changes on, or merge the PR unless explicitly requested.
