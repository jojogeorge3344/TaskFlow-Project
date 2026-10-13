# Design

## Context

Task management (Screens 7-8) is in flight: `task-management-impl` adds the `Task` and
`TaskRecipient` entities, `TaskStatus`/`TaskPriority` enums, the `AddTasks` migration, and
(when finished) `TaskService` plus list/create/status endpoints. The domain currently has no
`TaskComment`, `TaskActivityLog`, or `Notification` entities, no `TaskService`, and no
`TasksController`. See proposal.md - Why for motivation and the delta specs for the required
behavior.

Existing conventions this design follows (Program.cs, DepartmentsController, Infrastructure
services):
- Thin controllers under `/api/v1`; interface in `TaskFlow.Application`, implementation in
  `TaskFlow.Infrastructure.Services`, registered scoped in `Program.cs`.
- Role gates as named policies (`AuthPolicies.AdminOnly`, `ManagerOrAdmin`); only role-based
  authorization exists today - there is no resource-level handler yet.
- FluentValidation validators; DTOs as records; `InvalidOperationException` -> `BadRequest`.
- UTC timestamps, soft delete with `IsDeleted`, `AsNoTracking()` reads.
- Frontend: authenticated screens are children of the shell route, nav entries in
  `core/layout/nav-items.ts`, lazy-loaded feature components, Angular CDK already in the stack
  for drag & drop.

## Goals / Non-Goals

**Goals:**
- Resource-based access for every task-scoped action (assignee, creator, recipient, or
  Manager/Admin scope) - never trust client-supplied IDs alone.
- One immutable, chronological activity trail written from a single service.
- Status transitions as a pure domain rule reused by the board and task details.
- Board, comments, reassign, edit, and activity UIs wired as shell-child routes with the
  currently-disabled nav items enabled.

**Non-Goals:**
- Notification Center UI, reminder scheduling/SMTP, and settings screens (Screens 12-14, later
  module). Only the notification *write* seam for reassignment lives here.
- Workload and reports (Screens 16-17), profile and app settings (19-20).
- Recipient management screens beyond the details-page display and the additional-recipient
  editing on the edit form.

## Decisions

| # | Decision | Rationale | Alternatives |
|---|---|---|---|
| D1 | `TaskAccess` resource policy via an `IAuthorizationHandler` (`TaskAccessHandler`) that grants assignee, creator, recipient, or Manager/Admin; controllers call `IAuthorizationService` with the task id as resource | TDD names `TaskAccessHandler` (TDD §6.3); keeps authorization at the same layer as the existing role policies | Inline per-service `CanAccess(...)` checks - simpler but scatter authorization into services |
| D2 | One `TaskActivityService.AppendAsync(...)` used by task edit, status, reassign, and comment flows; no domain-event bus | Explicit call sites keep the change small and auditable; an outbox/event bus is overkill here | Domain events / outbox deferred (not needed at this scale) |
| D3 | Comments and activity are append-only: no update/delete service methods or endpoints | Matches "immutable" spec requirements and soft-delete history policy | Soft-delete mutations - contradicts immutability, no benefit |
| D4 | Stage (Upcoming / Due today / Overdue / Completed) and the overdue flag are derived at read time via a pure `TaskStageCalculator` in Application; never stored | `agent.md` rule: overdue is never stored; derived data cannot go stale | Stored flags - violates existing rule |
| D5 | Allowed status transitions in a pure Domain helper `TaskStatusTransitions.Can(from, to)` per TDD §10.2; both board and details use it; attempted disallowed changes rejected | Single source of truth for the rule, reused by both surfaces | Duplicated if-clauses in two services |
| D6 | Dedicated `GET /api/v1/tasks/board` returning all in-scope tasks; the frontend groups by status client-side | One request, no pagination pressure on board columns; list endpoint stays paginated for /tasks | Reusing paginated /tasks - boards would need multiple pages |
| D7 | Edit is `PUT /api/v1/tasks/{id}` (full update of editable fields) gated by Manager/Admin (creator is always one of those roles today), reusing create validators | TDD §8.4; symmetric contract with create | PATCH semantics - more surface for no user need |
| D8 | Reassignment: `POST /api/v1/tasks/{id}/reassign` with `{ newAssigneeUserId, reason }`; on success, update assignee, append `Reassigned` activity, refresh recipients so the assignee is always included, and publish a notification through `INotificationPublisher` | Screen 15 FR-6/7/8; recipients rule BR-5 is already modeled by `TaskRecipient` | Hard-coding recipient inserts in TaskService - couples responsibilities |
| D9 | Minimal `Notification` entity + append-only `INotificationPublisher` implementation added in this change so the reassignment notification is real; listing/filtering/UI arrive with the notification module | Keeps the spec's "new assignee is notified" scenario honest without building Screen 12 | Skipping the notification record - would silently defer a spec scenario |
| D10 | Frontend routes: `/tasks/:id` details, `/tasks/:id/discussion`, `/tasks/:id/reassign`, `/tasks/:id/activity`, `/board`; nav gains Task Management (Admin/Manager) and Board entries; "My Tasks" no longer disabled for Employees | Matches TDD §12.3; single shared details layout | Separate standalone screens - duplicates shared details header |
| D11 | Kanban drag/drop with Angular CDK `DragDrop`; drop calls the same status-update endpoint; optimistic UI with rollback on error | CDK already in stack (project.md §3); consistent UX with TDD | Move buttons/selects - worse UX, still needs the same endpoint |
| D12 | New EF migration adding `task_comments`, `task_activity_logs`, `notifications`; indexes on `(TaskId, CreatedAt)` for the two history tables | History reads are always per-task and chronological | No indexes on hot read path |

## Risks / Trade-offs

- **Ordering with `task-management-impl`** → This change builds on the `Task` entity and
  `AddTasks` migration; task-management-impl must be implemented first so TaskService/controller
  additions compose. Coordination: land this change's domain/API slices only after that one's
  migration exists.
- **Notifications created now, visible later** → Reassignment notifications are written to the
  DB this change, but the Notification Center UI is Screen 12 (later module), so they are not
  user-visible until then. Mitigation: verify via DB/tests; the notification module reads them.
- **Board payload growth** → `GET /tasks/board` returns every in-scope task. Acceptable for the
  mini-project dataset; if it grows, a status-scoped virtual scroll is a later refinement.
- **Last-write-wins on concurrent status changes** → Accepted; no optimistic concurrency for
  the mini project.
- **New schema (3 tables)** → Covered by one migration; SQLite; rollback by dropping the
  migration before release.

## Migration Plan

1. Domain: `TaskComment`, `TaskActivityLog`, `Notification` entities; `TaskStatusTransitions`
   and event/type enums.
2. Infrastructure: DbContext config + migration `AddTaskTracking`; apply via
   `dotnet ef database update`.
3. Application: DTOs/validators/interfaces; `TaskService` (details/edit/status/board),
   `TaskCommentService`, `TaskActivityService`, `TaskStageCalculator`,
   `INotificationPublisher` (+ implementation in Infrastructure).
4. API: `TaskAccessHandler` + `TaskAccess` policy; `TasksController` additions (board, edit,
   status, reassign, comments, activity, recipients GET/PUT).
5. Frontend: task API client/models; `TaskDetailsComponent`, `KanbanComponent`,
   `TaskDiscussionComponent`, `ReassignComponent`, `ActivityComponent`; routes + nav.
6. Verification: `dotnet test`, `ng test`, build both; manual smoke for details/edit/board/
   comments/reassign/activity as Manager and Employee.