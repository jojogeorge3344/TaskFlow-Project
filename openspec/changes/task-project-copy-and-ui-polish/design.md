# Design

## Context

Angular standalone components with signals on the front end, ASP.NET Core with EF Core on the back end. Observed state:

- `GET /tasks` already filters by `projectId` (`TaskService.ApplyEntityFilters`); the Task list screen has no control for it.
- `TaskListItemDto` and `TaskDetailsDto` carry no project fields, so the UI cannot show a project name today. `Task.ProjectId` exists and is nullable for legacy rows.
- The "edit task" screen is the inline edit mode of `task-details`. Its Back button is hardcoded: `backToBoard()` navigates to `/board`.
- `task-create` already pre-selects a project from `?projectId=`, which is the pattern to extend for copying.
- `project-team-dialog` renders headings (`.team-head`) and rows (`.team-row`) as two separate grids with the same five column widths, but the headings are uppercase and wider than their columns, and the manager cell is a checkbox with a label.
- The profile page was already restyled in the working tree by `modern-form-ui-refresh`; this change continues from that state.

See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Project name visible and filterable on task screens with minimal API change.
- Copy as a pre-filled create form that reuses the existing form and validation.
- A Back button that is correct for every entry point, survives reload, and is safe against open redirects.
- Layout polish for the edit form, team dialog and profile page using the shared `tf-*` primitives.

**Non-Goals:**
- Moving a task to another project, bulk copy, or copying comments, attachments or history.
- A new design system, dark theme, or changes outside the three screens named.
- Changing `GET /tasks` filter semantics.

## Decisions

**1. Project name via the DTOs, not a second lookup.** Add `ProjectId` (int?) and `ProjectName` (string?) to `TaskListItemDto` and `TaskDetailsDto`, filled by the existing task queries with a left join to projects. Alternative: the client resolves names from `GET /projects`. Rejected because callers without project access would show blanks, it costs an extra request, and deleted projects would be unresolved. Adding fields is backward compatible.

**2. Project filter data source.** The dropdown reuses `ProjectService.getAll()` and is shown only when that call succeeds. A failure hides the filter rather than blocking the list.

**3. Copy through `?copyFrom=<id>` on `/tasks/new`.** `task-create` loads the source with `TaskService.getById`, patches the form, and keeps the new-task date defaults. Alternative: a server-side "duplicate" endpoint that creates the task immediately. Rejected: it leaves orphan tasks when the user abandons the form, needs a second validation path, and creates before the user can change dates. Recipients come from the source's recipient list minus the assignee. Reminder settings are taken from the source if present on the details payload; otherwise the configured defaults apply. If the source's reminder fields are absent from `TaskDetailsDto`, the task adds them as part of the DTO work (see tasks). The `projectId` query still wins when both parameters are present.

**4. Back via a validated `returnUrl` query parameter.** Task links built in the Task list, Project detail and Kanban add `returnUrl` as a query parameter. A small shared helper validates it (must start with a single `/`, must not start with `//`, no scheme) and derives the label from the path prefix (`/projects/` → project, `/board` → board, `/tasks` → tasks). The task sub-screens (discussion, activity, reassign) and post-save navigation preserve the parameter. Alternatives: `history.back()` (breaks after edit, reload and deep links) and router state (lost on reload). The query parameter is the only option that survives all three.

**5. Edit form: restructure markup, keep the form model.** Keep `editForm` and its validators so behaviour and specs hold. Rebuild the template with `tf-card` sections, a CSS grid that goes to one column below the small breakpoint, and a sticky action bar. Project is rendered as read-only text outside the form group, so it cannot be posted.

**6. Team dialog alignment: one shared column template.** Define the column template once as a CSS custom property on the dialog, used by both the heading row and the data rows, so they cannot drift. Use sentence-case headings without letter-spacing, size the percent and manager columns from the heading text, and center the manager checkbox in its column. Below the small breakpoint, rows stack and each field carries a visible label (the heading row is hidden only because every field then labels itself). Alternative: a real `<table>`. Rejected, since the pickers are custom components and a table fights the responsive stacking.

**7. Profile layout: CSS grid with area placement.** Use a two- or three-column grid with sections ordered so heights balance, compact empty states for sparse sections, and collapse to one column on phones. No new component or markup restructure beyond section order and wrappers.

## Risks / Trade-offs

- [Open change overlap: `modern-form-ui-refresh` and `project-detail-and-task-project-required` touch the same files] → Build on the working tree, keep existing classes and `data-*` attributes, and run their specs.
- [The extra join in the task queries could slow the list] → It is a single-key left join on an indexed foreign key; check the query count in the DTO tests.
- [`returnUrl` could be used for an open redirect] → Strict in-app path validation, with unit tests for `//host`, `http://`, `javascript:` and empty values.
- [Copying a task whose assignee has been deactivated] → The assignee is dropped from the prefill and the existing required-field validation asks for a new one.
- [Long `returnUrl` values from a filtered list] → The URL stays well under limits because only the path and query of the list are stored; the list keeps its own filters only if they are already in its URL.
- [Task list filters are held in signals, not the URL, so Back to tasks shows default filters] → Accepted for this change. Persisting list filters in the URL is a separate improvement.
