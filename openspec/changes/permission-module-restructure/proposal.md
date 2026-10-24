# Proposal

## Why

The audit in `Artifacts/permission-audit/` shows the permission catalogue and Role Configuration tree are grouped differently from the menu users see. The side menu already shows TaskFlow Monitoring, TaskFlow Management, Communication Management, Report, Settings and My Profile, but the catalogue still uses twelve modules ("Tasks", "Projects", "Reports and workload" and so on). Board has no permission of its own, Employee Workload has only one key, and 156 of 412 recorded features cannot be controlled individually. The audit also found that `roles.manage` can be given to a custom role, which together with `employees.role.assign` lets that role hand out Administrator.

## What Changes

- Regroup the catalogue display into: **TaskFlow Monitoring** (Task Management, Employee Workload, Board), **TaskFlow Management** (Employee, Project, Department Management), **Communication Management** (Mail, Notification), **Report**, **Settings** (Reminder, Application, TFA, Role Configuration) and **My Profile**. Dashboard and the TFA assistant stay separate top-level modules, as decided.
- Rename "Tasks" to **Task Management** everywhere an administrator reads it (catalogue, Role Configuration tree and matrix).
- Add **Board** permissions, separate from Task Management: a view permission for the screen and a filters permission. They are seeded to every role that holds task access today, so no one loses the board.
- Add **Employee Workload** screen permissions: keep `workload.view` for the screen and add a filters permission and an "open employee's tasks" permission.
- Guard the `/board` and `/tasks/:id/activity` routes and hide their menu items and links without the matching key.
- Keep Role Configuration (`roles.manage`) assignable to custom roles, so a delegated role manager can create and edit custom roles, but reserve the privilege-raising permissions for the Administrator: only the Administrator can give a role Role Configuration or Assign roles.
- Add a new Administrator-only permission, `roles.administrator.assign`, required to give anyone the Administrator role through employee create or update, and stop employee update from accepting a password. Both close the privilege-escalation paths found in the audit.
- Let the employee form and role filter load roles for users who can assign roles but cannot open Role Configuration (today they get a 403 and a hardcoded fallback).
- Correct the stale comments the audit listed (`permissions.ts`, `SettingsController`, TFA settings, employee form hint).
- **No existing permission key is renamed or deleted.** New keys are additions; renames affect display names and grouping only.
- Not breaking: roles that already hold `roles.manage` keep it. A non-Administrator who holds it can no longer give a role Assign roles or Role Configuration, which was possible before.

## Capabilities

### New Capabilities

- `permission-module-hierarchy`: How the catalogue groups permissions into the six requested modules plus Dashboard and TFA assistant, naming, ordering, and the rule that grouping changes never change keys or effective access.
- `monitoring-screen-permissions`: Board and Employee Workload screen permissions, their route and menu gating, seeding to existing roles, and the Task Management activity route guard.
- `role-assignment-safeguards`: delegated Role Configuration with Administrator-only privilege-raising permissions, the permission needed to give the Administrator role, and removal of the password field from employee update.

### Modified Capabilities

<!-- None. openspec/specs/ is empty. The unsynced module-based-role-permissions change defines permission-catalog and role-configuration-ui, so this change references them rather than modifying them. -->

## Impact

- **Backend**:
  - Catalogue and keys: `PermissionCatalog.cs`, `PermissionKeys.cs`.
  - Seeding and role handling: `AccessControlSeeder.cs` (seed new keys to current holders), `RoleService.cs`.
  - Controllers: `TasksController` (board endpoint), `WorkloadController`, `UsersController` (role assignment, password field).
  - `RolesController` and `RoleService` (restricted keys), and a new `roles.administrator.assign` key.
  - A migration only if permission rows carry the group name; grouping may be derived instead (see design).
- **Frontend**: `permissions.ts`, `nav-items.ts`, `app.routes.ts`, board and workload components, task details link to activity, Role Configuration tree grouping, and the stale comments.
- **Tests**: catalogue integrity (the backend test that compares the two key lists), seeding keeps existing access for Administrator, Manager, Employee and custom roles, `roles.manage` rejected on custom roles, 403 for Administrator-role assignment without `roles.manage`, route guard and nav specs.
- **Data**: new permission rows and grants for current holders. No existing rows are removed.
- **Out of scope**: the other 150 or so uncontrolled features (dashboard cards and charts, task list and details view keys, the cross-module dependencies in `permission-gaps.md` section D, over-broad reads, per-user overrides). They remain in the audit as follow-up candidates.
