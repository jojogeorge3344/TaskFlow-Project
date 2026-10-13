# Design

## Context

Screens 7-8 (Task List, Create Task) are the last unimplemented slice of the task core. The domain
already landed under this change and its companion `task-tracking-impl`: the `Task`,
`TaskRecipient`, `TaskComment`, and `TaskActivityLog` entities, the `TaskStatus`/`TaskPriority`
enums, the `AddTasks`/`AddTaskTracking` migrations, the `AppDbContext` wiring, and the `TaskAccess`
resource policy (`TaskAccessHandler`). What is still missing is the create path (no `CreateAsync`
in `TaskService`, no `POST /tasks`) and the list path (no `ListAsync`, no `GET /tasks`, no
`TaskListComponent`/`TaskCreateComponent`, no `/tasks` or `/tasks/new` routes). `task-tracking-impl`
delivered details, board, comments, reassignment, and per-task activity and deliberately left
Screens 7-8 to this change (see proposal.md - Why).

Existing conventions this design follows (`Program.cs`, `DepartmentsController`, `TaskService`):
- Thin controllers under `/api/v1`, interface in `TaskFlow.Application`, implementation in
  `TaskFlow.Infrastructure.Services`, registered scoped in `Program.cs`.
- Role gates as named policies (`AuthPolicies.AdminOnly`, `ManagerOrAdmin`); row access through the
  existing `TaskAccess` policy.
- FluentValidation validators; DTOs as records; `InvalidOperationException` -> `BadRequest`.
- UTC timestamps, soft delete with `IsDeleted`, `AsNoTracking()` reads, derived (never stored)
  deadline state via `TaskStageCalculator`.
- Frontend: authenticated screens are children of the shell route with a `core/layout/nav-items.ts`
  entry, lazy-loaded, styled with the `src/styles.css` tokens and `.tf-*` primitives.

## Goals / Non-Goals

**Goals:**
- A real create path: request/validator, unique task code, creator + timestamps, recipients,
  reminder settings, initial activity, and the assignee notification.
- A real list path: role-scoped, paginated, searchable, filterable, with derived overdue/upcoming
  state.
- Task List and Create Task UIs wired as shell children that mirror server validation.

**Non-Goals:**
- The reminder scheduler, `reminder_history`, SMTP/email, and the Notification Center UI (Screens
  12-14, a later module). This change only captures per-task reminder *settings*.
- Workload, reports, profile, and application settings (Screens 16-20).
- Changing task details, board, comments, reassignment, or activity behavior delivered by
  `task-tracking-impl`; the details/status requirements carry over unchanged.

## Decisions

| # | Decision | Rationale | Alternatives |
|---|---|---|---|
| D1 | Contract: add `CreateTaskRequest` + `CreateTaskRequestValidator`, `ITaskService.CreateAsync`/`ListAsync`, `GET /api/v1/tasks` (paginated `{items,total,page,pageSize}`), and `POST /api/v1/tasks` | TDD §8.4 keeps the list paginated and the create endpoint separate from the board | Extending the board endpoint - it has no paging/filters and would bloat the board payload |
| D2 | Generate the task code from the identity `Id` after insert as `TSK-{Id:D6}` in the same transaction; the unique index on `TaskCode` is the backstop | Cannot collide, needs no extra state | A dedicated counter table - extra moving part for no benefit at this scale |
| D3 | Assignee is always inserted as a recipient; additional recipients are deduped against the assignee and each other, relying on the unique `(TaskId, UserId)` index | BR-5 and the existing `TaskRecipient` model; reuses `SetRecipientsAsync` semantics | Free-form recipient list - would allow duplicates and violate the rule |
| D4 | Add `FirstReminderDays`, `SecondReminderDays`, `OverdueReminderFrequency` to `Task` (TDD §7.2) with defaults 2 / 1 / Daily via migration `AddTaskReminderSettings`; the reminder engine (later module) consumes them | Screen 8 FR-9 captures reminder intervals at creation and TDD already specifies the columns | Deferring the columns to the reminder module - would leave Screen 8 with nowhere to store its selection |
| D5 | Creation side effects run in one transaction: insert task, insert recipients, append `Created` + `Assigned` activity via `ITaskActivityService`, publish a `TaskAssigned` notification via `INotificationPublisher` | Keeps the create flow atomic and reuses existing seams; aligns with `task-tracking-impl` D2 (no event bus) | Domain events / outbox - deferred, unnecessary at this scale |
| D6 | Add a dedicated `TaskListItemDto` (code, title, assignee, creator, priority, start, deadline, estimated hours, status, derived flags) rather than extending `TaskCardDto` | The list needs creator/start/estimated-hours that the board does not; keeps the board contract stable | Extending `TaskCardDto` - changes the board payload for no list benefit |
| D7 | Derive overdue/upcoming and stage at read time with the existing `TaskStageCalculator`; never store them | `agent.md` rule: overdue is never stored, so it cannot go stale | Stored flags - violate the rule and would drift |
| D8 | Add `/tasks` (list) and `/tasks/new` (create, Manager/Admin). Retarget the Admin/Manager "Task Management" and Employee "My Tasks" nav entries from `/board` to `/tasks`, and link from the list to `/board` | Screen 7 is the canonical list and hosts the Create button; a list-first landing is the natural entry | Keeping `/board` as the landing - leaves the list and create flow unreachable from the menu |
| D9 | `POST /tasks` is gated `ManagerOrAdmin`; `GET /tasks` is authenticated and role-scoped in the query (Employee -> assigned/creator/recipient, Manager/Admin -> accessible); row access reuses `TaskAccess` | TDD §6.3/§8.4; never trust client-supplied IDs for ownership | Inline `CanAccess` checks - scatter authorization into services |

## Risks / Trade-offs

- **Retargeting nav touches completed `task-tracking-impl` behavior** -> keep `/board` reachable
  from the list; verify the board still renders after the nav change.
- **Task code generation under concurrency** -> the `Id`-based format cannot collide; the unique
  index on `TaskCode` is the final guard.
- **Reminder columns added here but unconsumed until the reminder module** -> defaults keep every
  task valid; the later module must consume these columns rather than re-add them.
- **List payload growth once demo data widens** -> server-side paging, filtering, and search keep
  responses bounded.
- **Overlap with `task-tracking-impl` specs for details/status** -> this change owns list/create;
  the details/status requirements here carry over unchanged.

## Migration Plan

1. Domain/Application: `CreateTaskRequest`, `TaskListFilter`, `TaskListItemDto`,
   `CreateTaskRequestValidator`; reminder columns on `Task`.
2. Infrastructure: migration `AddTaskReminderSettings`; `TaskService.CreateAsync`/`ListAsync`.
3. API: `POST /tasks`, `GET /tasks`.
4. Frontend: models + `TaskService.list/create`; `TaskListComponent`, `TaskCreateComponent`;
   routes + nav.
5. Verification: `dotnet test`, `ng test`, both builds; manual smoke as Manager and Employee.

## Open Questions

- The exact "upcoming" window on the list (for example due today versus within a reminder horizon)
  can be finalized with the reminder module; the spec only requires that future, non-terminal
  deadlines are shown as upcoming.
