# Proposal

## Why

Administrators cannot remove an employee or a task: both entities carry an `IsDeleted` flag but no endpoint or UI sets it. Managers also have no recorded link to departments or employees, so admins cannot express who manages whom. Finally, employee emails are free-form, so addresses on mixed domains (the seed data uses `@taskflow.local`) coexist and two people can end up with near-identical addresses. A single fixed company domain makes accounts consistent and unambiguous.

## What Changes

- Add an Administrator-only **soft delete** for employees (sets `IsDeleted`). An admin cannot delete themselves or the last active administrator. Deleted users stop signing in and disappear from lists and assignee pickers; their historical tasks, comments and mail are kept.
- Add an Administrator-only **soft delete** for tasks (sets `IsDeleted`). Deleted tasks vanish from lists, boards, reports, workload and dashboards; activity history is retained.
- Add **manager assignments**, editable by Administrators only:
  - a manager can be assigned to **multiple departments**;
  - an employee can be assigned to **one manager**.
  - This is recorded data only; manager visibility of tasks, workload and dashboards is **not** changed in this change.
- Enforce a fixed employee email domain **`@taskflow.com`**:
  - the UI shows `@taskflow.com` as static, non-editable text and the user types only the local part;
  - the server composes and validates `<local>@taskflow.com` and rejects any submitted address whose domain differs;
  - uniqueness is checked on the local part, case-insensitively, so the same local part on any domain is a duplicate;
  - existing users are migrated by rewriting their domain to `@taskflow.com`; when two users would collide, later users get a numeric suffix on the local part.
- Seed accounts and the documented login credentials move from `@taskflow.local` to `@taskflow.com`.
- **BREAKING**: existing users must sign in with the converted `@taskflow.com` address; the old addresses stop working.

## Capabilities

### New Capabilities
- `employee-deletion`: Administrator-only soft delete of employees, with self and last-admin protection and effects on sign-in, lists and pickers.
- `task-deletion`: Administrator-only soft delete of tasks and its effect on every task view.
- `manager-assignments`: Admin-managed assignment of multiple departments to a manager and of employees to a manager (data only).
- `employee-email-domain`: Fixed `@taskflow.com` email domain, static suffix in the UI, local-part uniqueness, and migration of existing addresses.

### Modified Capabilities

None. `openspec/specs/` holds no main specs yet, so all behavior is introduced as new capabilities.

## Impact

- **Backend**: `UsersController`, `TasksController`, `UserService`, `TaskService`, `DepartmentService`/department queries, create/update user validators, `PermissionCatalog`/`PermissionKeys` (delete is Administrator-only, not an assignable permission), `User` entity (nullable `ManagerId`), a new manager-department join entity, `AppDbContext`, one EF migration (schema plus email rewrite), `DatabaseSeeder` emails.
- **Frontend**: employees screen (delete action, manager and manager-departments fields, email local-part input with static suffix), task details/list (delete action, admin only), role-aware UI guards.
- **Docs and data**: `Login creds.txt`, README references, tests that use `@taskflow.local`.
- **No change** to manager task, workload or dashboard visibility.
