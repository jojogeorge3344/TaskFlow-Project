## Why

Role Configuration is a flat list of 19 coarse permissions in 9 groups. One key often covers many unrelated actions (`employees.manage` creates, edits, deactivates, resets MFA and changes roles; `projects.manage` creates, edits, deletes and manages members), and two destructive actions are guarded by the wrong key (task delete and user delete use `roles.manage`). Dashboards are split by audience (`dashboard.admin`, `dashboard.manager`, an open employee dashboard) instead of by what a user may see. Administrators cannot express "may view the board but not delete tasks" or "may export reports but not view workload", and the screen does not scale past a handful of checkboxes.

## What Changes

- Replace the flat catalogue with one **module-based catalogue**: Module -> Screen -> Tab/Section -> Feature -> Action. Each module appears exactly once; no per-role copies. Inventory is taken from the real controllers, routes and screens (about 120 leaf permissions across 12 modules: Dashboard, Tasks, Projects, Departments, Employees, Reports & Workload, Mail, Notifications, AI Assistant, Settings, Role Configuration, Profile).
- Keep the existing 19 keys as **parent permissions**. A role holding an old key is expanded to all of that key's child permissions in a data migration, so no current access is lost or widened.
- Dashboard becomes a single module. The dashboard view permission carries a **data scope** (Own, Team, Organization) that maps onto the existing employee, manager and admin endpoints, instead of separate role dashboards. Individual widgets/sections and filters get their own permissions.
- Fix mis-keyed actions: task delete and user delete get real permissions (`tasks.delete`, `employees.delete`). **BREAKING** for custom roles only if they relied on `roles.manage`, which is Administrator-only and not assignable, so no custom role is affected.
- Align route guards with API checks (`/departments` and `/employees` currently require `.manage` while the API needs only `.view`).
- Enforce every protected action on the backend with `[RequirePermission]`, hide unauthorized menus, tabs, buttons and widgets in the SPA, and block direct URL access with `permissionGuard`.
- Redesign the Role Configuration screen: role rail, module navigator with live counts, tri-state checkboxes at module/screen/feature level, Select all / Clear all, search, Enabled/Disabled filter, expand/collapse, scope selector, copy-from-role, "Duplicate as custom" for system roles, optional role-by-permission matrix view, sticky save bar, responsive master/detail layout that becomes drill-in on mobile.
- System roles stay read-only and non-deletable; custom roles never alter system role templates.
- Self-service areas with no restriction today (own Profile, Mail, Notifications, assistant chat) are catalogued so they are visible, but default to granted for every role and are marked locked-on in this change (see design for the open item).

## Capabilities

### New Capabilities
- `permission-catalog`: The single module-based permission catalogue (structure, keys, parent/child relation, scope settings, default grants for system roles), backend enforcement per action, and migration of existing role grants.
- `role-configuration-ui`: The redesigned Role Configuration screen and the frontend enforcement it drives (menu, route, tab, button and widget visibility).

### Modified Capabilities
<!-- None: openspec/specs/ is empty; the earlier role-permission-management change is complete but unsynced, so its requirements are referenced, not modified. -->

## Impact

- **Backend**: `PermissionCatalog` / `PermissionKeys` (module tree and child keys), `AccessControlSeeder` (insert new keys, expand held parent keys for existing roles, keep Administrator = everything), new EF migration for catalogue metadata (module, screen, section, action, scope) and grant expansion, `RoleService`/`RoleDtos` (tree DTO, scope grants), `RequirePermission` on `TasksController`, `UsersController`, `ProjectsController`, `DepartmentsController`, `DashboardController`, `ReportController`, `SettingsController`, `MailController`, `NotificationsController`, `AssistantController`, `MfaController`; dashboard and assistant services that read scope.
- **Frontend**: `core/auth/permissions.ts`, `nav-items.ts`, `app.routes.ts`, `auth.guard`, `role.service`/`role.model`, dashboard, task, project, department, employee, report, mail and settings components (button/tab/widget gating), and a rewritten `permissions-management` component.
- **Tests**: catalogue integrity (no duplicates, every endpoint keyed, every key in the tree), migration preserves effective access for Administrator/Manager/Employee and custom roles, API 403 per protected action, route guard and nav specs, role screen component specs.
- **Data**: new permission rows and expanded `RolePermissions`; existing role assignments untouched.
- **Out of scope**: Notification Preferences, task progress percentage and an "Approve" action (not present in the application today), user-level permission overrides, permission audit log.
