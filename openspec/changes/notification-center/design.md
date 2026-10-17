# Design

## Context

See `proposal.md` for motivation. The current state that shapes the approach:

- The notification **write** seam already exists from `task-tracking-impl`: the `Notification`
  entity (`Id`, `UserId`, `TaskId?`, `Type`, `Message`, `IsRead`, `CreatedAt`), the
  `NotificationType` enum, an append-only `INotificationPublisher`, the `notifications` table and
  a composite index on `(UserId, IsRead)`. `TaskService` publishes `TaskAssigned` on create and
  `Reassigned` on reassignment, inside the task transaction.
- There is **no read path**: no notification query service, no `NotificationsController`, no
  frontend service, no UI, and no `bell` in the fixed `ICONS` map of
  `frontend/src/app/core/layout/icon.component.ts`.
- The shell header (`shell.component.html`) currently renders only the menu toggle, brand and
  title. The approved header mockup (`app-shell-and-admin-ui/mockup.html:305`) already places a
  bell button with an unread dot on the right of the header.
- Conventions (project.md §8, agent.md §4): base path `/api/v1`; thin controllers delegating to
  Application services; list responses `{ items, total, page, pageSize }` (page 1 / pageSize 20 by
  default, per `TaskListFilter`); `ProblemDetails` errors; explicit `[Authorize]` policies; caller
  identity from `ClaimTypes.NameIdentifier`; frontend uses signals for state and RxJS for HTTP;
  new authenticated screens register as shell children and add a nav entry.

Observed discrepancy (not resolved here): `project.md` §6 lists `NotificationType` as
`TaskAssigned, Reminder, DueToday, Overdue, Reassigned, Comment`, but the shipped enum is
`TaskAssigned, Reassigned, DeadlineReminder, Overdue, System`. This change creates no notifications
and reads whatever type string is stored, so it is unaffected either way.

## Goals / Non-Goals

**Goals:**
- A read path for the authenticated user's own notifications: a paged, newest-first, filterable
  list, an unread count, mark-one and mark-all-as-read.
- A bell with an unread badge in the shell header for all three roles, and a Notification Center
  screen that lists entries, filters them, marks them read, and opens the related task.
- Reuse the existing entity, table and index unchanged; no new dependencies.

**Non-Goals:**
- Generating reminder / due-today / overdue notifications, reminder history and deduplication, and
  email delivery — all owned by the Screen 13/14 module.
- Real-time push (SignalR), notification preferences, and any change to the write seam or
  `NotificationType`.
- Changing the write-side requirements owned by the in-flight `task-management` and
  `task-tracking-impl` deltas.

## Decisions

**D1 - Read the existing table; no schema change.** The `(UserId, IsRead)` composite index already
serves both the paged list and the unread count, and `(UserId, CreatedAt)` ordering is cheap at
this scale. Alternatives: adding a dedicated index or denormalised counter — rejected as premature;
the index is sufficient and avoids a migration.

**D2 - Endpoint surface.** Four endpoints, all `[Authorize]`:
`GET /api/v1/notifications` (paged, filter by `type` and `isRead`),
`GET /api/v1/notifications/unread-count`,
`POST /api/v1/notifications/{id}/read`, and `POST /api/v1/notifications/read-all`.
Action-style `POST` for read/read-all matches the existing `POST /tasks/{id}/reassign` idiom.
Alternatives: `PATCH /notifications/{id}` with `{ isRead }` is more RESTful and mirrors
`PATCH /tasks/{id}/status`, but exposes a general update surface and still needs a separate bulk
route; the explicit action endpoints are narrower and self-documenting.

**D3 - Ownership is a query predicate, not a policy handler.** The list, unread count and mark-read
always filter `UserId == callerId`, where `callerId` comes from the caller's token. Mark-read
resolves the row by `(id, UserId)`; a row that is not the caller's is reported as not found, so it
cannot be distinguished from a nonexistent one. Alternatives: a resource-based
`TaskAccess`-style authorization handler — overkill for a single-column ownership rule, and more
indirection than the existing `TryGetCurrentUser` controller pattern used by Dashboard and Tasks.

**D4 - Response shape reuses `PagedResult<T>`.** The list returns
`PagedResult<NotificationDto>` -> `{ items, total, page, pageSize }` (page 1, pageSize 20), matching
the task list. `NotificationDto` carries `Id`, `Type`, `Message`, `IsRead`, `CreatedAt`, `TaskId?`
and `TaskCode?`. Including `TaskCode` lets a row show the code, consistent with `TaskListItemDto`.
`Type` is serialised as the enum name string (the entity already stores it as a string).

**D5 - Soft-deleted tasks present as "no related task".** The `Notification -> Task` relation is
optional and tasks are soft-deleted. When a notification's related task is soft-deleted, the DTO
returns `TaskId = null` so the UI shows a plain entry instead of a link that would 404. Alternative:
keep the id and let the task route handle it — rejected as a broken-link experience.

**D6 - One shared `NotificationService` owns the unread count.** A root-provided service in
`core/services/` holds an `unreadCount` signal; the shell refreshes it on each navigation and on
the existing 60s cadence, and the center updates it after mark-read / mark-all. Alternatives: the
shell polling the endpoint privately — rejected, since the center also needs the count and the two
would drift; a full state library — unnecessary for one scalar.

**D7 - Bell in the header, screen via `RouterLink`; plus a nav entry.** The header gains a bell
button styled like the mockup's `.icon-btn` with a dot when `unreadCount() > 0`, linking to
`/notifications`. The route is registered as a shell child for all roles and a `Notifications` nav
item (bell icon) is added to each role in `ROLE_NAVIGATION`, which also makes `pageTitle()` resolve
the header title. Alternative: header-only, no sidebar entry — rejected because the project
convention is to register a menu entry for each authenticated screen.

**D8 - Extract `bell` into the fixed icon map.** Add the Lucide bell path to `ICONS` in
`icon.component.ts`, keeping the trusted-constant, `currentColor` approach. The mockup already
references `#i-bell`.

**D9 - The center is a lazy-loaded feature page.** `features/notifications/notification-center`
component with the paged list, type / read-state filters, per-item "mark as read", "mark all as
read", and entries rendered as links to `/tasks/:id` when a task is present. The existing
`.tf-*` primitives and design tokens are reused; no CSS framework. Alternatives: a header dropdown
panel — rejected; the screen spec lists filters and a bulk action that need room.

**D10 - Test the isolation boundary.** Integration tests use two users: one cannot list, count or
mark the other's notifications. This is the security-critical behaviour from the spec, so it is
tested at the API boundary rather than only at the service.

## Risks / Trade-offs

- [The center looks sparse until Screen 13 lands, because only `TaskAssigned` / `Reassigned` rows
  exist] → Accept and document: those two are real producers today (create and reassign), so the
  screen is demonstrable; reminder-driven rows simply appear once the reminder module ships.
- [Seeded data may not contain notifications, so a fresh run could show an empty center] →
  Manual verification creates a task or reassigns one to produce a row; integration tests seed
  notifications directly.
- [Referencing a soft-deleted task as a link] → Mitigated by D5 (return `TaskId = null`).
- [Badge polling adds periodic requests] → Reuse the shell's existing navigation-triggered refresh
  and 60s cadence rather than a new fast timer; the count endpoint is a single indexed aggregate.
- [`project.md` enum drift] → Not corrected in this change (out of scope); flagged so the Screen 13
  module reconciles it when the reminder types gain producers.

## Migration Plan

No schema change and no data migration: the `notifications` table and index already exist, and the
change only adds read endpoints and UI. Deployment is an API + SPA rebuild. Rollback is reverting
the commit; the table is untouched either way.

## Open Questions

- Whether the unread badge should eventually update in real time (SignalR) instead of on
  navigation + interval — deferred; the current design does not block adding it later.
