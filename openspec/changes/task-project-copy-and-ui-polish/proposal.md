## Why

Task screens still do not show which project a task belongs to, the Task list cannot be narrowed to one project, and an existing task cannot be duplicated. Navigating back from a task always lands on the Kanban board, even when the user came from a project or the task list. Three screens also look unfinished: the task edit form is a plain stack of fields, the project team dialog's column headings do not line up with their inputs, and the profile page leaves large areas empty.

## What Changes

- **Project filter on the Task list.** A Project dropdown joins the existing filters and sends `projectId` (already supported by `GET /tasks`). It resets with the other filters.
- **Project name on tasks.** `TaskListItemDto` and `TaskDetailsDto` gain `ProjectId` and `ProjectName` (null for legacy tasks with no project). The Task list shows a Project column, and the task screen shows the project name in the header and in the edit form, read-only. Moving a task to another project is out of scope.
- **Copy task.** A "Copy" action on each Task list row and on the task screen opens `/tasks/new?copyFrom=<id>`: the Create Task form pre-filled from the source task (title, description, project, assignee, priority, estimated hours, recipients, reminder settings). Nothing is saved until the user submits. Dates restart at now and now + 3 days, and status is To Do as for any new task.
- **Redesigned task edit screen.** The inline edit form becomes a sectioned, responsive card layout built from the shared `tf-*` primitives: a header with task code and project, a details section, a schedule section, and a sticky action bar. It stacks to one column on phones.
- **Aligned team dialog headings.** The headings (Employee, Role, Allocation %, Project manager) share the row grid exactly, use sentence case without overflow, and the manager checkbox aligns under its heading. On narrow widths each row labels its own fields instead of hiding the headings.
- **Context-aware Back.** Links into a task carry a `returnUrl` query parameter (from the Task list, Project detail and Board). The Back button goes to `returnUrl` when it is a valid in-app path, and falls back to the Task list otherwise. Its label names the destination ("Back to project", "Back to tasks", "Back to board").
- **Profile page layout.** Rework the profile layout so cards fill the available width in a balanced multi-column grid with no large empty areas, and the page stays elegant and responsive down to phone width.

## Capabilities

### New Capabilities
- `task-project-visibility`: project name on task list and task screens, plus the project filter on the Task list.
- `task-copy`: duplicating an existing task into a pre-filled Create Task form.
- `task-navigation-return`: Back button returns to the screen the user came from.
- `task-edit-screen-ui`: layout and responsiveness rules for the task edit form.
- `project-team-dialog-alignment`: heading and row alignment and responsive behaviour of the assign-team dialog. Kept separate from the in-flight `project-team-dialog-ux` so the two do not collide at archive.
- `profile-page-layout`: space-filling, responsive layout rules for the profile page.

### Modified Capabilities
<!-- None: no main specs exist under openspec/specs/. -->

## Impact

- Backend: `TaskDtos.cs` (`TaskListItemDto`, `TaskDetailsDto`), `TaskService` mappers (join to project name). No migration; `Task.ProjectId` already exists.
- Frontend: `task-list` (filter, column, Copy action, `returnUrl` on links), `task-details` (project name, redesigned edit form, Back logic), `task-create` (`copyFrom` prefill), `project-detail` and `kanban` (`returnUrl` on task links), `project-team-dialog` (grid and headings), `profile` (template and CSS), `task.model.ts`.
- Tests: updates to task-list, task-details, task-create, project-team-dialog and profile specs; new tests for `returnUrl` validation and copy prefill; a backend test for project name in the DTOs.

## Assumptions

- The in-flight changes `project-detail-and-task-project-required` and `modern-form-ui-refresh` cover the same screens. This change builds on their working-tree code, and their remaining manual-verification tasks are not repeated here.
- Users who can create tasks (`tasks.create`) can copy; the Copy action is hidden for everyone else.
- Copy keeps the source's assignee even if that user has since been deactivated; the create form's existing validation then asks the user to pick another.
- The project filter lists the projects the caller already sees through `GET /projects`. Callers without that permission do not see the filter.
- `returnUrl` is accepted only when it starts with a single `/`, to prevent open redirects.
