## 1. Backend: project required on task creation

- [x] 1.1 Add `int ProjectId` to `CreateTaskRequest` (`TaskRequests.cs`) and update all constructors/callers
- [x] 1.2 Add `ProjectId > 0` rule ("A project is required.") to `CreateTaskRequestValidator`
- [x] 1.3 In `TaskService.CreateAsync`, verify the project exists and is not deleted (throw `InvalidOperationException` otherwise) and set `Task.ProjectId`
- [x] 1.4 Assistant action tools: no task-creation action exists (only profile/role/etc.), so nothing to change
- [x] 1.5 Backend tests: validator (missing project), service (unknown/deleted/valid project), existing create tests updated

## 2. Backend: project tasks endpoint

- [x] 2.1 Add `ProjectTaskDto` and `ProjectTaskFilter` (status, priority, deadlineFrom, deadlineTo, assignedToUserId, positionId) to `TaskFlow.Application/Projects`
- [x] 2.2 Add `GetTasksAsync(projectId, filter)` to `IProjectService` and implement in `ProjectService` (non-deleted tasks, join assignee's membership for position, apply filters, order by deadline)
- [x] 2.3 Add `GET /projects/{id}/tasks` to `ProjectsController` (`ProjectsView`, 404 for unknown project)
- [x] 2.4 Service tests for filters, role filter and unknown project (null → 404)

## 3. Frontend: mandatory project on Create Task

- [x] 3.1 Add `projectId` to `CreateTaskRequest` in `core/models/task.model.ts`
- [x] 3.2 Add required Project select (loaded via `ProjectService.getAll()`) to `task-create` component/template with "Project is required." error; pre-select from `?projectId=`
- [x] 3.3 Send `projectId` in `submit()`; handle project-load failure state
- [x] 3.4 Update `task-create.component.spec.ts` (required validation, request payload)

## 4. Frontend: Project Detail screen

- [x] 4.1 Add `ProjectTask` model and `ProjectService.getTasks(id, filters)` plus spec
- [x] 4.2 Create `features/projects/project-detail.component` (.ts/.html/.css): header with stat chips, Team panel, Tasks panel with filter bar (status, priority, deadline from/to, user, role) and Clear action
- [x] 4.3 Build user/role filter options from `getMembers()` (roles = distinct positions on this project); apply filters through the API
- [x] 4.4 Loading, error, empty-team, empty-tasks and no-match states; "Project not found" for 404
- [x] 4.5 Responsive styles (two columns on desktop, stacked cards on phone), reuse `tf-*` tokens, overdue tint, dark-mode safe
- [x] 4.6 Register route `projects/:id` (lazy, `permissionGuard` with `ProjectsView`) in `app.routes.ts` and update `app.routes.spec.ts`
- [x] 4.7 Make project cards in `project-list` link to the detail route without breaking the action buttons; update `project-list.component.spec.ts`
- [x] 4.8 Write `project-detail.component.spec.ts` (renders team and tasks, role options limited to project roles, filters call the API)

## 5. Frontend: team dialog headings and role placeholder

- [x] 5.1 Add heading row (Employee, Role, Allocation %, Project manager) in `project-team-dialog` sharing the row grid columns; show only when rows exist, hide on phone width
- [x] 5.2 Remove `defaultPositionId()` so new rows start with no role
- [x] 5.3 Add `placeholder` input to `PositionComboboxComponent` (default unchanged) and pass "Role in this project" from the dialog
- [x] 5.4 Update `project-team-dialog.component.spec.ts` and any position-combobox spec (no "Member" default, placeholder, headings)

## 6. Verification

- [x] 6.1 Run backend build and tests; run frontend lint/tests/build
- [ ] 6.2 Manually verify: create task blocked without project, project card opens detail, filters (incl. role) work, team dialog headings/placeholder
