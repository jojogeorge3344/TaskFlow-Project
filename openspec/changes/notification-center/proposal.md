# Proposal

## Why

Task creation and reassignment already write in-app notifications to the `notifications`
table, but nothing reads them. `INotificationPublisher` is deliberately append-only, and both
`task-tracking-impl` (design D9) and `task-management-impl` explicitly deferred "the Notification
Center UI (Screens 12-14)" to a later module. The result is that assignment and reassignment
alerts exist as dead rows: users cannot see them, count them, or clear them. Screen 12 turns the
existing write seam into a usable capability, and it is the natural home for the reminder-driven
notifications the deadline workflow will later produce.

## What Changes

Backend:
- Application: add a notification read service (a page of the caller's notifications with
  filters, an unread count, and mark-one / mark-all-as-read) with DTOs, scoped strictly to the
  authenticated user.
- API: `GET /api/v1/notifications` (paged; filter by type and read state),
  `GET /api/v1/notifications/unread-count`, `POST /api/v1/notifications/{id}/read`, and
  `POST /api/v1/notifications/read-all`. Every endpoint is `[Authorize]` and resolves ownership
  from the caller's token — never from a client-supplied id.
- No schema change: the `notifications` table, `Notification` entity, `NotificationType` enum
  and `(UserId, IsRead)` index already exist from `task-tracking-impl`. This change adds reads
  and mark-as-read only.

Frontend:
- Add a `bell` icon to the shared icon set and a notification bell with an unread badge to the
  shell header (the approved header mockup already shows this control with a dot).
- Add a `NotificationService` (core) and a Notification Center feature: a paged list showing
  message, type, timestamp and a related-task link, with per-item mark-as-read, mark-all-as-read,
  and type / read-state filters. Opening the related task navigates to `/tasks/:id`.
- Register the Notifications route as a shell child for all three roles and add the nav entry
  (currently absent).

Out of scope, and left to the Screen 13 reminder module: generation of near-deadline, due-today
and overdue reminders, reminder history and duplicate prevention, and the `DeadlineReminder` /
`Overdue` producers. This change deliberately surfaces only the `TaskAssigned` and `Reassigned`
notifications that already exist.

## Capabilities

### New Capabilities
- `notification-center`: Reading and clearing a user's own in-app notifications — a paged,
  filterable list, an unread count, mark-as-read / mark-all-as-read, and per-item navigation to
  the related task, with strict per-user isolation.

### Modified Capabilities
_None_ — `openspec list --specs` shows no synced main specs yet. The notification *write* seam
(`TaskAssigned`, `Reassigned`) is owned by the in-flight `task-management` and `task-tracking-impl`
deltas; this change adds the read/center behavior only and does not change those requirements.

## Impact

- Backend Application: new `INotificationService` + implementation and DTOs (reusing
  `PagedResult<T>`); DI registration in `Program.cs`.
- API: new `NotificationsController` under `/api/v1/notifications`.
- Frontend core: `NotificationService` + models; a `bell` icon in `core/layout/icon.component.ts`;
  bell + badge in `core/layout/shell.component.*`; the Notifications entry in `app.routes.ts` and
  `core/layout/nav-items.ts`.
- Frontend feature: a new `features/notifications/` component.
- Database: no migration (the table and the `(UserId, IsRead)` index already exist).
- Tests: integration tests for scoped listing, unread count, mark-read / mark-all, and cross-user
  denial, plus component specs for the center and the header badge.
- No new external dependencies.
