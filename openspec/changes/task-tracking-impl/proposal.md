# Proposal

## Why

Task management gives us a task list and creation flow (Screens 7-8, in flight), but tracking
stops there. Screens 9, 10, 11, 15 and 18 turn that list into a usable tracking system: full
task details with editing, a Kanban board, threaded comments, manager-driven reassignment, and
an immutable activity history. Without these, users can create tasks but cannot follow,
discuss, reassign, or audit them.

## What Changes

Backend:
- Domain: add `TaskComment` and `TaskActivityLog` entities + migration (currently absent;
  only `Task`/`TaskRecipient` exist).
- `TaskService`: extend with task editing (title, description, priority, start date, deadline,
  estimated hours), deadline/priority change handling, derived stage computation
  (Upcoming / DueToday / Overdue / Completed) at read time, and reassignment.
- `TaskCommentService`: add/list comments, immutable, author + timestamp, non-empty validation.
- `TaskActivityService`: append + query immutable activity records (create, assign, reassign,
  status, deadline, priority, completion, admin actions).
- API: `PUT /tasks/{id}`, `POST /tasks/{id}/reassign`, `GET/POST /tasks/{id}/comments`,
  `GET /tasks/{id}/activity`, board-oriented listing (status grouping), all gated by the task
  resource-access policy (assignee, creator, recipient, or Manager).
- Resource-based authorization: a `TaskAccessHandler`-style policy so scoped actions never
  trust client-supplied IDs alone.

Frontend:
- `/tasks/:id` Task Details — full details, derived stage badge, edit (Manager/Admin), status
  control, recipients list, embedded comments and activity.
- `/board` Kanban board — status columns, cards (id, title, assignee, priority, deadline),
  drag/drop status change, overdue highlight, refresh.
- `/tasks/:id/discussion` comments screen.
- `/tasks/:id/reassign` reassignment screen (new assignee, reason, confirm/cancel).
- `/tasks/:id/activity` activity history with filters.
- Wire routes + enable the currently-disabled "Task Management"/"My Tasks" nav items.

## Capabilities

### New Capabilities
- `task-board`: Kanban board that groups tasks by status, surfaces overdue work, and routes
  board-driven status changes through validation and activity recording.
- `task-comments`: Chronological, immutable task discussion with author and timestamp, open to
  authorized task participants.
- `task-lifecycle`: Managing an existing task after creation — full details, derived stage,
  editing by Managers/Administrators, notification-recipient visibility, and no reminders for
  terminal tasks.
- `task-reassignment`: Manager-initiated transfer of a task to another employee with a reason,
  history preserved and recipients updated.
- `task-activity-history`: An immutable, chronological record of important task changes,
  queryable with filters and never modifiable by ordinary users.

### Modified Capabilities
_None_ — `openspec list --specs` shows no synced main specs yet (each capability above is new;
the in-flight `task-management` delta from `task-management-impl` stays owned by that change,
and tasks shared by Screens 9/10 like viewing details and basic status updates already live
there).

## Impact

- Backend Domain: `TaskComment`, `TaskActivityLog` entities + EF migration; stage derivation.
- Application services: `TaskService`, `TaskCommentService`, `TaskActivityService`,
  validators, DTOs.
- API: `TasksController` additions under `/api/v1/tasks` (edit, status, reassign, comments,
  activity, board listing).
- Frontend: `features/tasks` (details, board, discussion, reassign, activity components),
  routes in `app.routes.ts`, nav entries in `core/layout/nav-items.ts`.
- No new external dependencies; Angular CDK drag & drop already in the stack (project.md §3).
- Database schema additions via a new migration.