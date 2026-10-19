# Design

## Context

See proposal.md for motivation. Current state, as observed in the code:

- `User`, `Task` and `Department` already have `IsDeleted`, and services filter on it (`UserService`, `TaskService`, `DashboardService`, `ReportService`, `WorkloadService`, `ReminderService`, `NotificationService`, `TaskAccessHandler`). No endpoint sets the flag for users or tasks.
- Access control is permission-based (`PermissionCatalog`, `[RequirePermission]`). `roles.manage` is the one non-assignable, Administrator-only permission and is the precedent for "admin only, not grantable".
- `User.DepartmentId` is a single FK. There is no manager link of any kind; manager visibility comes from `tasks.view.all` (organization-wide).
- `UserService` already has `EnsureAnotherActiveAdministratorAsync` and `EndSessionsAsync` (security stamp and refresh tokens).
- Email uniqueness is a case-insensitive full-address match that ignores deleted users (`!u.IsDeleted`). Sign-in accepts email or username. Seeds use `@taskflow.local`.
- Database is SQLite with EF Core migrations; the latest is `AddAiModels`.

## Goals / Non-Goals

**Goals:**
- Soft delete for users and tasks, enforced on the server and mirrored in the UI.
- Persist manager-to-department (many-to-many) and employee-to-manager (many-to-one) as data.
- One server-side source of truth for the email domain, with the UI only mirroring it.
- A migration that converts existing emails deterministically and idempotently.

**Non-Goals:**
- Scoping manager visibility to assigned departments or employees (decided: data only).
- Hard delete, undelete/restore, or a recycle-bin screen.
- Making the email domain configurable at runtime or per tenant; it is a constant.
- Changing usernames or the login flow.

## Decisions

**D1. Administrator-only delete is checked by role, not by a new permission.**
Delete endpoints use an Administrator-role policy, modeled on `roles.manage` (not assignable). Adding `employees.delete` / `tasks.delete` permissions was rejected: the requirement is that no other role can ever hold it, and the permission matrix UI would invite granting it. Existing `employees.manage` and `tasks.update` do not imply delete.

**D2. Endpoints.** `DELETE /api/users/{id}`, `DELETE /api/tasks/{id}`, both 204 on success, 404 when missing or already deleted, 400 with `{ error }` for rule violations (matching the existing `InvalidOperationException` to `BadRequest` pattern), 403 for non-admins.

**D3. Deleting a user.**
In one transaction: reject self-delete and last active admin (reuse `EnsureAnotherActiveAdministratorAsync`); reject when the user still has unfinished assigned tasks (the message asks for reassignment, consistent with the existing "avoid accidental deletion when related task records exist" rule); `IsDeleted = true`, `IsActive = false`; end sessions via `EndSessionsAsync`; clear `ManagerId` on their reports and remove their manager-department rows. The email is left as is, so it stays reserved.
Alternative considered: auto-reassigning open tasks. Rejected because the right assignee is a business choice.
Sign-in and token refresh already exclude deleted users where they filter `IsDeleted`; verify the login and refresh queries do, and fix if not.

**D3b. Deleted users' names in history.** Queries that join to the author or assignee must not drop rows when the user is deleted. Where a join currently filters `!IsDeleted` on the user, keep returning the task or comment and show the stored name.

**D4. Deleting a task.**
Set `IsDeleted = true` and write an activity entry (actor, time) using the existing activity mechanism. Comments and activity rows are kept. Reminder, notification, workload, report and dashboard queries already filter `!IsDeleted`; add tests rather than assume. Assistant tools must filter the same way. Unread notifications referencing the task are left, and opening one returns 404.

**D5. Manager assignment data model.**
- `User.ManagerId` (nullable self-FK, `OnDelete(SetNull)`) for employee-to-manager.
- New join entity `ManagerDepartment(ManagerUserId, DepartmentId)` with a composite key, cascade from both sides, for manager-to-department.
Alternatives: a `Department.ManagerId` (one manager per department, contradicts "multiple managers per department" being allowed and the many-to-many need); reusing `DepartmentId` on managers (single value only). Both rejected.

**D6. Manager assignment API.**
A dedicated `PUT /api/v1/users/{id}/assignments` with `{ managerId, managerDepartmentIds }`, gated like the delete endpoints (`roles.manage`, Administrator-only), and both fields explicit with full-replace semantics. `UserDto` is extended (read side) with `ManagerId`, `ManagerName`, `ManagerDepartments` and, for managers, `Reports`. Extending the create/update user requests was rejected: a null `ManagerId` there cannot tell "clear" from "not sent", so an older client saving a user would silently wipe assignments, and a non-admin holding `employees.manage` would need a field-level guard. The employee form calls the new endpoint after a successful save when the signed-in user is an Administrator. Validation: target of `ManagerId` must be an active, non-deleted Manager and not the user themselves; `ManagerDepartmentIds` requires the user to be a Manager and the departments to exist. Changing a manager's role away from Manager clears both. Saving replaces the department set.

**D7. Email domain: one constant, composed on the server.**
A single `EmailPolicy` in the Application layer holds `Domain = "taskflow.com"` and `Normalize(input)`, `IsValidLocalPart`. Requests keep an `Email` string for compatibility; the server lowercases, requires the domain to equal `taskflow.com` (rejecting others, rather than silently rewriting, so API callers learn about it), and validates the local part with a regex (`^[a-z0-9_-]+(\.[a-z0-9_-]+)*$`, length 1-64). The UI sends `<local>@taskflow.com`.
Alternative: have the API accept only a local part. Rejected because it breaks the existing contract and `UserDto.Email` shape for no gain.

**D8. Uniqueness.**
Compare the local part case-insensitively against all users including deleted ones (the Users query filter is bypassed for this check). Because the domain is fixed, this equals full-address uniqueness after migration. The existing unique index on `Email` stays as the backstop against a race; every stored address is lowercase, so a NOCASE collation was not added. Deleted users keep their email and still block reuse, which keeps history unambiguous.

**D9. Migration of existing emails.**
Do it in the EF migration with SQL: for each user ordered by `Id` (creation order), local part = text before the last `@` lowercased; if already taken by an earlier user, append `2`, `3`, ... until free; set `Email = local || '@taskflow.com'`. Done before the unique index is created. Users with no `@` keep the whole value as the local part. Idempotent because a second run produces the same addresses. Seeds in `DatabaseSeeder` switch to `@taskflow.com`; the seeder matches existing seed users by username as well as email, so it will not re-create them.
Down migration is not able to restore original domains; this is accepted and stated in Risks.

**D10. Frontend.**
- Employee form: an input group with the local part and a static `@taskflow.com` addon; input handler strips everything from the first `@`, lowercases and trims. Same in the profile screen. Domain constant lives in one shared file.
- Delete buttons shown only when the signed-in user is an Administrator (role check like the existing admin guards, not a permission claim), with a confirmation dialog and server error messages displayed.
- Employee form gains Manager (select of active managers) for non-managers and Departments (multi-select) for managers; details panel lists them.

## Risks / Trade-offs

- **Email conversion is lossy and breaking** → users must use the new address; the migration is logged with a before/after list, and sign-in by username keeps working. Collision suffixes (`jo2`) may surprise people; the admin can edit the local part afterwards.
- **Migration cannot be reversed to original domains** → back up the database first (the storage change already warns about leftover databases); document in the release note.
- **Soft-deleted users still reserve emails and usernames** → intentional; an admin cannot reuse a name, which avoids ambiguity in history. Revisit if restore is wanted.
- **Blocking deletion when open tasks exist** → admin must reassign first; an extra step, but avoids orphaned work.
- **Some queries may not yet filter deleted tasks or users** (reminders, notifications, assistant) → each is covered by an explicit test in the task list.
- **Unique index on email can fail the migration if the rewrite leaves a duplicate** → the rewrite step runs first and is collision-free by construction; case differences are handled by lowercasing.
- **Visibility not scoped** → managers still see everything, so the new assignments are informational until a later change.
