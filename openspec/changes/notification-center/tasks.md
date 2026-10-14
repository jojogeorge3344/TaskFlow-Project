# Tasks

## 1. Backend notification read path

- [x] 1.1 Add `NotificationDto` (Id, Type, Message, IsRead, CreatedAt, TaskId?, TaskCode?) and a `NotificationFilter` (Type?, IsRead?, Page, PageSize) to `TaskFlow.Application`, and declare `INotificationService` with `ListAsync(userId, filter)`, `GetUnreadCountAsync(userId)`, `MarkReadAsync(userId, id)` and `MarkAllReadAsync(userId)`; verify `dotnet build` succeeds.
- [x] 1.2 Implement `NotificationService` in `TaskFlow.Infrastructure` over `AppDbContext`: filter every query by `UserId`, page newest-first (`CreatedAt` descending), project `TaskCode` via the related task, and return `TaskId = null` when the related task is missing or soft-deleted (design D5). Register the service in `Program.cs` alongside the other Application services; verify `dotnet build` succeeds.
- [x] 1.3 Add `NotificationsController` (`/api/v1/notifications`, `[Authorize]`, thin) exposing `GET`, `GET unread-count`, `POST {id}/read` and `POST read-all`, resolving the caller from `ClaimTypes.NameIdentifier` and returning `404` when `MarkReadAsync` finds no owned row; verify `dotnet build` succeeds.
- [x] 1.4 Add unit tests for `NotificationService`: list excludes another user's rows, unread count counts only unread owned rows, mark-read is idempotent, mark-all clears the count, and a soft-deleted related task yields `TaskId = null`; verify `dotnet test` passes.
- [x] 1.5 Add integration tests for the controller with two seeded users: paged `{ items, total, page, pageSize }` shape and newest-first order, `type` / `isRead` filters, unread count, mark-read persists, mark-all zeroes the count, a cross-user mark-read returns `404` and leaves the row unchanged, and an unauthenticated call returns `401`; verify `dotnet test` passes.

## 2. Frontend data layer

- [x] 2.1 Add notification models (`NotificationItem` with id, type, message, isRead, createdAt, taskId?, taskCode?, and a `NotificationFilter`) in `core/models`, exported from `core/models/index.ts`; verify `npm run build` succeeds.
- [x] 2.2 Add a root-provided `NotificationService` in `core/services` wrapping the four endpoints and holding an `unreadCount` signal with `refreshUnreadCount()`, `markRead()` and `markAllRead()` that update the signal; verify `npm run build` succeeds.
- [x] 2.3 Add a `NotificationService` spec covering query-param construction, signal update on `refreshUnreadCount`, and the count decrease after `markRead` / reset after `markAllRead` (design D6); verify `ng test` passes.

## 3. Shell chrome: bell and unread badge

- [x] 3.1 Add a `bell` entry to the fixed `ICONS` map in `core/layout/icon.component.ts` (design D8); verify the existing icon and shell specs still pass.
- [x] 3.2 Add the bell button with an unread dot to the shell header, bound to `NotificationService.unreadCount()`, linking to `/notifications`, and refresh the count on navigation and on the existing 60s cadence (design D6/D7); verify a shell spec shows the dot when the count is greater than zero and hides it at zero.
- [x] 3.3 Register `/notifications` as a shell child route for all three roles and add the `Notifications` (bell icon) entry to each role in `ROLE_NAVIGATION` (design D7); verify the route spec recognises the path and the header title resolves to "Notifications".

## 4. Notification Center screen

- [x] 4.1 Create the lazy-loaded `features/notifications` component: a paged list showing type, message, timestamp and the related task code, with type / read-state filters, per-item "mark as read", a "mark all as read" action, and rows linking to `/tasks/:id` when a task is present, using the existing `.tf-*` primitives (design D9); verify `npm run build` succeeds.
- [x] 4.2 Add component specs: applying a filter narrows the list, marking one read updates the shared unread count, mark-all zeroes it, and a task-related row targets `/tasks/:id`; verify `ng test` passes.

## 5. Integration verification

- [x] 5.1 Run `dotnet build` and `dotnet test` at the backend root and `npm run build` and `ng test` in `frontend/`; verify all pass.
- [x] 5.2 Manual smoke in `npm start` as two roles: create a task and reassign one to produce `TaskAssigned` / `Reassigned` rows, open the center, filter, mark one read, mark all read, and open the related task; verify behaviour against Screen 12 and the header mockup at 390px, 768px and 1280px.
- [x] 5.3 Confirm the docs stay accurate: check `project.md` §5/§8 for any statement that Screen 12 is unimplemented and correct it, noting that reminder-driven notifications remain with the Screen 13 module; verify no stale "Notification Center UI (later module)" note contradicts the shipped read path.

## Workflow follow-up

- Archive the change after review requirements are satisfied.
