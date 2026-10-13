# Tasks

## 1. Domain & data foundation

- [x] 1.1 Add `TaskPriority` and `TaskStatus` enums and verify they match the TDD values.
- [x] 1.2 Add the `Task` entity and the `TaskRecipient`/`TaskComment` entities and verify the Domain project builds.
- [x] 1.3 Register the task sets and relationships in `AppDbContext` and verify the Infrastructure project builds.
- [x] 1.4 Create the `AddTasks`/`AddTaskTracking` migrations and verify `dotnet ef database update` creates the task tables and indexes.
- [x] 1.5 Add `FirstReminderDays`, `SecondReminderDays`, and `OverdueReminderFrequency` to `Task` with defaults 2 / 1 / Daily and verify migration `AddTaskReminderSettings` applies with existing rows keeping the defaults.

## 2. Create task (backend)

- [x] 2.1 Add `CreateTaskRequest` and a `CreateTaskRequestValidator` (required title/assignee/deadline, deadline after start, estimated hours > 0, valid priority) and verify unit tests cover each rejection.
- [x] 2.2 Implement `ITaskService.CreateAsync` to generate the task code, insert the task with creator and timestamps, and add the assignee plus additional recipients deduped against the unique `(TaskId, UserId)` index; verify unit tests cover a unique code and recipient dedup.
- [x] 2.3 Record the initial `Created` and `Assigned` activity through `ITaskActivityService` during creation and verify the entries appear for the new task.
- [x] 2.4 Publish a `TaskAssigned` notification to the assignee through `INotificationPublisher` and verify the notification row is created.
- [x] 2.5 Add `POST /api/v1/tasks` gated by `ManagerOrAdmin` returning the created task and verify integration tests for 201, employee 403, and validation 400.

## 3. Task list (backend)

- [x] 3.1 Add `TaskListFilter` (employee, status, priority, deadline from/to, search, page, pageSize) and `TaskListItemDto`, and implement `ITaskService.ListAsync` returning `{items,total,page,pageSize}` scoped by role; verify unit tests for Employee versus Manager scope.
- [x] 3.2 Add `GET /api/v1/tasks` applying filters, search, and paging and verify integration tests for assignee scope, employee/status filter, deadline-range filter, search by code/title, and paging metadata.
- [x] 3.3 Expose derived deadline state on list rows via `TaskStageCalculator` and verify unit tests for overdue and upcoming states (never stored).

## 4. Task list (frontend)

- [x] 4.1 Add `task.model.ts` list/create types and `TaskService.list()`/`create()` and verify `task.service.spec.ts` covers both calls.
- [x] 4.2 Build `TaskListComponent` at `/tasks` with the `.tf-table` columns (code, title, assignee, creator, priority, start, deadline, estimated hours, status), a debounced search, employee/status/priority/deadline filters, paging, overdue/upcoming badges, loading/empty/error states, and a "Create task" button; verify with a component spec.
- [x] 4.3 Register `/tasks` as a shell child and retarget the Admin/Manager "Task Management" and Employee "My Tasks" nav entries to `/tasks` with a link to `/board`; verify `app.routes.spec.ts` and that the board still loads.

## 5. Create task (frontend)

- [x] 5.1 Build `TaskCreateComponent` at `/tasks/new` (reactive form: title, description, assignee, priority, start, deadline, estimated hours, additional recipients, reminder options) with inline validation mirroring the server and verify with a component spec for each required and field error.
- [x] 5.2 Register `/tasks/new` behind the Manager/Admin guard and verify success navigates to the created task's details with a success toast, and that an Employee is blocked.

## 6. Integration verification

- [x] 6.1 Run `dotnet test` and `ng test --watch=false` plus both production builds and verify all pass.
- [ ] 6.2 Manual smoke as Manager: create a task, find it via search and each filter, open its details; as Employee: confirm it is visible and not modifiable; confirm `/board` still renders.

## Workflow follow-up

- Archive the change after the project's review requirements are satisfied.
- Verify the archived result.
