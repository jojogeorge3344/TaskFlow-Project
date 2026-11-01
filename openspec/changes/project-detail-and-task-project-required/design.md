# Design

## Context

- `Task.ProjectId` is already a nullable FK, and `TaskListFilter` already has `ProjectId`, but `CreateTaskRequest` has no project, so tasks are created projectless.
- Project Management (`project-list`) shows cards with no detail route; the only routes are `/projects` and `/employees?projectId=`.
- Team data already exists: `GET /projects/{id}/members` returns `ProjectMemberDto` (user, position, percent, manager flags). Positions are the "roles" on a project.
- `project-team-dialog` renders rows in a 5-column grid (`employee | position | percent | manager | remove`) and seeds new rows via `defaultPositionId()` (the "Member" position). `position-combobox` hard-codes the placeholder "Pick or type a position".
- Frontend is Angular standalone components with signals; backend is ASP.NET with services behind controllers and FluentValidation.

## Goals / Non-Goals

**Goals:**
- Make a project mandatory at task creation, enforced on both client and server.
- Add a project detail screen with team, tasks and filters that matches the app's visual language.
- Add column headings to the team dialog and drop the "Member" default in favour of a placeholder.

**Non-Goals:**
- Backfilling or forcing a project on existing tasks; no migration, column stays nullable.
- Restricting assignees to project members, or changing task editing/reassignment rules.
- Changing allocation/percent rules or the manager-flag rules.
- Task create/edit from the project detail screen.

## Decisions

1. **Required project: validate in both layers, keep the column nullable.** Add `int ProjectId` to `CreateTaskRequest`; the validator requires `> 0` ("A project is required."), and `TaskService.CreateAsync` checks the project exists and is not deleted (`InvalidOperationException`, mapped to 400 like the assignee check). The DB column stays nullable so legacy rows remain valid. Alternative (NOT NULL + backfill) rejected: no sensible backfill value and it risks existing data.
   The assistant action tools that create tasks must supply a project too; they are updated to ask for/resolve one.

2. **Task create form:** a required Project `<select>` fed by `ProjectService.getAll()`, placed first. Same `showError` pattern as the other fields. Query param `?projectId=` pre-selects it (cheap hook for a later "add task" link from the detail screen).

3. **Detail route `/projects/:id`**, lazy `ProjectDetailComponent`, `permissionGuard` with `ProjectsView`. The project card title/body becomes a `routerLink`; the existing action buttons (team/edit/delete, View employees) keep working and stop click propagation so they do not navigate.

4. **Project tasks endpoint `GET /projects/{id}/tasks`** instead of reusing `GET /tasks?projectId=`. The existing list does not return the assignee's project role and its scoping rules differ per caller; a dedicated endpoint returns `ProjectTaskDto` (id, code, title, status, priority, start, deadline, assignee id/name, assignee position id/name) and supports `status`, `priority`, `deadlineFrom`, `deadlineTo`, `assignedToUserId`, `positionId`. The role filter is a join to the project's members: tasks whose assignee holds that position on this project. Returns 404 for an unknown project. Result is unpaged for now (a project's task count is small); add paging if it proves large.

5. **Filter options come from the team, not global lists:** the user filter lists project members; the role filter lists the distinct positions held on this project (from `/members`), which is exactly "only the roles assigned to this project". Status/priority use the existing enums. Filters are applied server-side via query params, debounced on change, with a Clear action and an empty-result state.

6. **Screen layout (modern, elegant):** back link; header card with project name, description, and stat chips (members, tasks, overdue, completed %); two-column body on desktop - left a Team panel (avatar initials, name, role pill, allocation bar with %; no manager badge, the team dialog shows and sets the flag), right the Tasks panel with a sticky filter bar and a table (code, title, assignee + role pill, priority badge, status badge, deadline with overdue tint). Below ~768px the columns stack and the table collapses into cards. Reuses existing `tf-*` tokens/classes and badge styles from the task list; dark mode comes with the tokens.

7. **Team dialog headings:** a header row using the same grid template as `.team-row` (shared CSS variable/class so columns stay aligned), rendered with `@if (rows().length > 0)`, labels Employee, Role, Allocation %, Project manager. On phone width, where the row collapses to two columns, the header row is hidden and the inputs keep their `aria-label`s.

8. **No default "Member":** remove `defaultPositionId()`; new rows start with `positionId: null`. `PositionComboboxComponent` gains a `placeholder` input (default unchanged for other users) and the dialog passes "Role in this project". Existing "required role" validation (`incomplete()`) already flags the empty field on save.

## Risks / Trade-offs

- **Existing tests/flows that create tasks without a project** (task-create spec, task service spec, assistant tools) will break → update them in the same change.
- **Task visibility on the detail screen:** the dedicated endpoint exposes all project tasks to anyone with `ProjectsView`, which may be broader than the per-role scoping on `GET /tasks`. Assumed acceptable; if not, scope the endpoint the same way (open question for review).
- **Assignee not on the team:** their role shows "—" and the role filter cannot match them; accepted since assignee restriction is a non-goal.
- **Unpaged list** could grow; mitigated by filters and a later paging option.
- Legacy projectless tasks never appear on any project screen until someone edits them (editing project is not offered here).
