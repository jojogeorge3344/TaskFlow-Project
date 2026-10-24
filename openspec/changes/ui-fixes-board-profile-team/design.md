# Design

## Context

- The board (`kanban.component.ts`) loads every task the caller may see from `GET /api/v1/tasks/board` and filters on the client (assignee, status, deadline) in `rebuild()`/`matches()`. `TaskCardDto` has no project, and `GetBoardAsync` only includes `AssignedTo`.
- The task list already has a project filter fed by `ProjectService.getAll()`; the board can reuse that source.
- The team dialog row (`project-team-dialog.component.ts`) is `<label class="team-row__manager"><input type="checkbox"/><span>Manager</span></label>` under a "Project manager" column heading.
- The profile (`profile.component.ts`) builds `readonlyFields()` as `{label, value: string}` with `.join(', ')`. The template renders two columns on desktop (left: Personal information + Account details, right: Password) and a Profile/Security tab switcher below the tablet breakpoint.
- See proposal.md for motivation. The in-flight `task-project-copy-and-ui-polish` change has an unfinished profile grid task (7.1) that touches the same template and CSS.

## Goals / Non-Goals

**Goals:**
- Add the project filter without a new endpoint or schema change.
- Keep the existing client-side filtering model so all filters behave the same way.
- Change the profile fields to render lists with no behaviour change to editing or saving.

**Non-Goals:**
- Server-side board filtering or paging.
- Changing who can be a project manager or the "at least one manager" rule.
- Redesigning other profile styling beyond column placement.

## Decisions

1. **Filter the board on the client, add project to the card payload.** Add `ProjectId` and `ProjectName` to `TaskCardDto`/`TaskCard`, add `.Include(task => task.Project)` in `GetBoardAsync`, and map them in `ToCard`. Alternative: pass `projectId` to `/board`. Rejected: the board already holds everything and filters locally, and a server round trip per change would make the Project filter behave differently from its neighbours.
2. **Project options come from `ProjectService.getAll()`**, as in the task list, shown for everyone who can open the board. Projects with no visible tasks still appear, and the existing "no tasks match" state covers them. Alternative: derive options from the loaded cards. Rejected: it would hide valid projects and differ from the task list.
3. **Project name on the card** is a small secondary line, since the filter alone does not tell people which project a card belongs to when "All projects" is chosen.
4. **Team dialog: remove only the `<span>Manager</span>`.** The checkbox keeps `aria-label`, `title` and disabled logic; a column heading already names it. Alternative: remove the control and derive managers from role. Rejected by the user, because the flag is the source of truth for project managers.
5. **Profile fields carry `values: string[]` instead of `value: string`.** `readonlyFields()` returns a list per field; the template renders a `<ul>` with bullets when there is more than one value, a plain line for one, and "—" for none. Single-value fields (Username, Role, Status, Employee code) stay as text. Projects become one item per allocation (`name · percent%`), with the total shown once beneath the list. Alternative: keep joined text and add line breaks. Rejected: it is not a real list for assistive technology.
6. **Profile columns: swap placement.** Left column: Account details. Right column: Personal information, then Password. On small screens the single column stacks in the same order. The mobile tab switcher is dropped so every section is reachable by scrolling; keeping it would hide Personal information behind a tab for no benefit now that the page is short.

## Risks / Trade-offs

- [Overlap with `task-project-copy-and-ui-polish` task 7.1 on profile layout] → Apply after, or together with, that task and keep one grid; the specs here only fix which section sits in which column.
- [Board payload grows by a project name per card] → Negligible for the current data size; revisit if the board gets paging.
- [Removing the tab switcher changes the mobile flow] → Sections stack in one column; the page stays short, so no section is hard to reach.
- [Existing profile specs assert comma-joined text and the tab switcher] → Update those specs in the same task as the template change.
- [Visible label removal could hurt clarity] → The "Project manager" column heading stays and the checkbox keeps its accessible name.
