## Why

Tasks can currently be created without a project, so work is not traceable to a project. Projects also have no page of their own: there is no place to see who works on a project, in which role, and what tasks belong to it. The team-assignment dialog is missing column headings and pre-fills every new row with a "Member" position nobody asked for.

## What Changes

- **Task creation requires a project.** The Create Task form gets a required Project field, and `POST /tasks` rejects a request with no project (or an unknown/deleted one). Existing tasks that have no project are left untouched and stay editable.
- **New Project Detail screen**, opened by clicking a project card in Project Management (`/projects/:id`). It shows the project header, the assigned team (name, role/position, allocation %, manager flag), and the project's tasks with their details, in a modern, elegant layout. Filters: status, priority, deadline date range, assigned user, and role. The role filter lists only the roles held on that project.
- **New project-tasks API** `GET /projects/{id}/tasks` (filterable by status, priority, deadline range, assignee and position/role) that also returns each assignee's role on the project.
- **Assign-team dialog gets column headings** above the rows: Employee, Role, Allocation %, Project manager.
- **Assign-team dialog stops pre-filling "Member".** New rows start with an empty role field whose placeholder reads "Role in this project".

## Capabilities

### New Capabilities
- `task-project-requirement`: a task cannot be created without a valid project.
- `project-detail-view`: project detail screen with team, mapped tasks and the filters above, plus its supporting API.
- `project-team-dialog-ux`: column headings and role-field default/placeholder in the assign-team dialog.

### Modified Capabilities
<!-- None: no main specs exist yet under openspec/specs/. The in-flight `user-project-allocations` change owns the base behaviour of the team-assignment dialog; this change only adjusts its presentation. -->

## Impact

- Backend: `CreateTaskRequest` (+ `ProjectId`), `CreateTaskRequestValidator`, `TaskService.CreateAsync` (project existence check), `ProjectsController` + `IProjectService`/`ProjectService` (new tasks endpoint and DTOs). No schema migration: `Task.ProjectId` already exists and stays nullable for legacy rows.
- Frontend: `task-create` component (Project select), new `project-detail` component and route `projects/:id` (guarded by `ProjectsView`), `project-list` cards become links, `ProjectService` (+ tasks call), `project-team-dialog` (headings, no default position), `position-combobox` (placeholder input), models.
- Tests: updates to task-create, project-team-dialog, project-list and routes specs; new specs for the detail screen and the validator.
- Assumptions: anyone with `ProjectsView` may open the detail screen and see all of the project's tasks; a task's assignee need not be a team member (their role then shows as "—"); other task-creation entry points (e.g. the assistant action tools) must also supply a project.
