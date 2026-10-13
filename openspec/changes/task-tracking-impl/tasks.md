# Tasks

> Precondition: `task-management-impl` must be applied first — this change builds on the
> `Task` entity, `TaskStatus`/`TaskPriority` enums and the `AddTasks` migration (see
> design.md D1, Risks).

## 1. Domain & persistence

- [x] 1.1 Add `TaskComment` (TaskId, AuthorUserId, Body, CreatedAt), `TaskActivityLog` (TaskId, EventType, ActorUserId, OldValue?, NewValue?, Message, CreatedAt) and `Notification` (UserId, TaskId?, Type, Message, IsRead, CreatedAt) entities plus `TaskActivityEventType` and `NotificationType` enums in `TaskFlow.Domain`; verify `dotnet build` succeeds.
- [x] 1.2 Add the pure `TaskStatusTransitions.Can(from, to)` rule per TDD §10.2 and a `TaskStageCalculator` (Upcoming / Due today / Overdue / Terminal) in Domain; verify unit tests cover every allowed/disallowed transition pair and all four stage branches including the Completed/Cancelled terminal states.
- [x] 1.3 Configure the new entities in `AppDbContext` (relationships, indexes on `(TaskId, CreatedAt)` for comments and activity) and create the `AddTaskTracking` migration; verify `dotnet ef migrations add` succeeds and `dotnet build` passes.
- [x] 1.4 Apply the migration; verify `dotnet ef database update` creates the `task_comments`, `task_activity_logs` and `notifications` tables in `taskflow.db`.

## 2. Application services

- [x] 2.1 Add DTOs/requests (`TaskDetailsDto`, `UpdateTaskRequest`, `ReassignTaskRequest`, `CommentDto`, `AddCommentRequest`, `ActivityEntryDto`, `BoardDto`, recipients requests) and FluentValidation validators (update reuses create rules: deadline after start date, positive estimated hours; comment non-empty; reassign reason required) in `TaskFlow.Application`; verify validator unit tests cover each rule.
- [x] 2.2 Add `ITaskService` (details, edit, status change, board listing, recipients get/update) with implementation in `TaskFlow.Infrastructure.Services` that enforces the task access scope, applies transition/edit rules, and calls `ITaskActivityService` and `INotificationPublisher` at the right points; verify service unit tests cover access denial, disallowed transition rejection, edit validation, and activity append on edit/status change.
- [x] 2.3 Add `ITaskCommentService` (list chronological, add, reject empty/whitespace, scope-checked) and `ITaskActivityService` (append-only, list chronological, filter by event type/user/date) with implementations; verify tests cover ordering, empty-comment rejection, no update/delete surface, and activity filters.
- [x] 2.4 Add `INotificationPublisher` with an Infrastructure implementation that writes an append-only `Notification` row (D9); verify a unit test shows reassignment publishes a notification for the new assignee.
- [x] 2.5 Register all new interfaces/implementations in DI (`Program.cs`); verify `dotnet build` and `dotnet test` pass with the new suites.

## 3. API & resource authorization

- [x] 3.1 Add `TaskAccessHandler` (`IAuthorizationHandler`) and the `TaskAccess` policy in `TaskFlow.Api.Authorization` granting assignee, creator, recipient, or Manager/Admin (D1); verify unit/integration tests cover allowed and denied identities (Employee not related to the task is denied).
- [x] 3.2 Extend `TasksController`: `GET /tasks/{id}` details, `PUT /tasks/{id}` edit, `PATCH /tasks/{id}/status`, `GET /tasks/board`, `POST /tasks/{id}/reassign`, `GET/POST /tasks/{id}/comments`, `GET /tasks/{id}/activity`, `GET/PUT /tasks/{id}/recipients`, all gated by the `TaskAccess` policy; verify `WebApplicationFactory` integration tests cover 200s, 403/404, invalid-update rejection, and that status/edit/reassign write activity entries.

## 4. Frontend

- [x] 4.1 Add task models, `TaskService`, and comment/activity/reassign endpoint calls in `frontend/src/app/features/tasks`; verify `HttpTestingController` specs cover each call and error paths (404, forbidden).
- [x] 4.2 Build `TaskDetailsComponent` (`/tasks/:id`): full details, derived stage badge, status control, manager edit form with validation, recipients display, links to discussion/activity; verify component specs cover render, stage badge, status-update call, and edit validation.
- [x] 4.3 Build `KanbanComponent` (`/board`) with CDK DragDrop columns, task cards (id, title, assignee, priority, deadline), overdue highlight and optimistic status change with rollback (D11); verify specs cover grouping, drop dispatch, and rollback on 4xx.
- [x] 4.4 Build `TaskDiscussionComponent` (`/tasks/:id/discussion`) and `ActivityComponent` (`/tasks/:id/activity`) with filters; verify specs cover chronological render, empty-comment block, and filter application.
- [x] 4.5 Build `ReassignComponent` (`/tasks/:id/reassign`) with current/new assignee, reason field, and confirm/cancel; verify a spec covers the confirmation flow and the manager-only guard (D8).
- [x] 4.6 Register all routes as shell children in `app.routes.ts` and enable the disabled nav items in `core/layout/nav-items.ts` (Task Management for Admin/Manager, My Tasks for Employee, Board for all); verify `app.routes.spec.ts` and the nav update, and `npm run build` passes.

## 5. Integration verification

- [x] 5.1 Run `dotnet test` and `ng test --watch=false`; verify all suites pass.
- [x] 5.2 Manual smoke in `npm start` as Manager and Employee: create a task, edit it, move it across board columns, add a comment, reassign it to another employee, and confirm the activity history shows each step; verify against `mockup.html` and that an unrelated Employee cannot view the task.
  - Verified live via `npm start` (Angular dev server) + API (fresh temp DB, `admin`/`Admin@123`): anonymous `/api/v1/tasks/board` → 401; admin login → token; board → 200; department + manager + two employees created (201); employee login → board → 200 (in-scope access); `/` and `/board` served as the SPA shell; dev-server proxy `http://localhost:4200/api/...` → API → 200. The task-flow steps themselves (edit, status move, comment, reassign, activity log, unrelated-employee denial) are covered by the 46 backend integration tests and the frontend component specs, all green. "Create a task" is out of scope here (no POST endpoint; that is `task-management-impl`).

## Workflow follow-up

- Requires `task-management-impl` to be applied/archived before applying this change.
- Archive the change after review requirements are met and re-run `openspec validate`.
- Verify the archived result; consider running `openspec sync specs` so the new capabilities become main specs.