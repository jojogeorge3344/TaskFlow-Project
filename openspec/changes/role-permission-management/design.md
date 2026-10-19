# Design

## Context

Observed today: SQLite via EF Core; `DatabasePathResolver` resolves `Data Source=../database/taskflow.db` against the solution folder, and `AiProviders:KeyRingPath` and `Mail:AttachmentsPath` also point into `../database`. `User.Role` is a `Role` enum stored as a string. The JWT carries one `ClaimTypes.Role` claim. `Program.cs` defines `AdminOnly`, `ManagerOrAdmin` and `TaskAccess` policies; about 30 controller actions use them. Row-level scoping reads the role name in `TaskAccessScope`, `TaskAccessHandler` and `WorkloadService`; `UserService` filters by enum; assistant tools and the authorizer read the role. On the SPA, `app.routes.ts` guards routes with `data: { roles: [...] }` and `role-routes.ts`/`nav-items.ts` pick a home route and menu per role. `DatabaseSeeder` seeds only the administrator and `Seed:Enabled` is `false`. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- One storage root (`C:\TaskFlow`) for the database, key ring and attachments, created on startup.
- Roles and permissions as data, with locked system roles and a fixed catalog.
- Every existing endpoint and route keeps its current behavior for the three system roles.
- Role/permission changes take effect on the next request.

**Non-Goals:**
- Admin-defined permissions (decided: fixed catalog only).
- Per-user permission grants or multiple roles per user.
- Migrating data from the old `database/` folder.
- Changing task row-level visibility rules beyond expressing them as permissions.
- A permission audit log or approval workflow.

## Decisions

**1. Storage root is a configuration value, resolved once at startup.**
Add `Storage:Root` (default `C:\TaskFlow`). A small resolver builds the database path, key ring path and attachments path from it, replacing the relative `../database` values. The folder is created before the DbContext is first used; failure to create or write aborts startup with a clear message. Alternative: keep relative paths and add a symlink or installer step. Rejected: it does not meet "created when the app runs", and relative paths depend on where the process starts. Tests override the root to a temp folder.

**2. Roles become rows; `User.RoleId` replaces the enum.**
Tables: `Roles(Id, Name unique case-insensitive, IsSystem)`, `Permissions(Id, Key unique, Name, Description, IsSystem)`, `RolePermissions(RoleId, PermissionId)`. System roles keep stable keys so code can find them without matching display names. The JWT keeps a role-name claim for display and for the Administrator-only gate, but carries no permissions. Alternative: keep the enum and add a nullable custom role. Rejected: two sources of truth for one decision.

**3. Permissions are resolved from the database per request, not stored in the token.**
The JWT bearer validation hook (`SessionValidation`) already loads the user on every request to check the security stamp. It now also loads the role name and the role's permission keys and replaces the token's role claim and adds one `permission` claim per key on the request's principal. A single `[RequirePermission("tasks.create")]` attribute maps to a policy per catalog key that requires that claim, replacing the role-name policies. This satisfies "change applies without sign-in" with no cache to invalidate. Services that scope data (`TaskService`, `WorkloadService`) look up the named role's grants in the database on each call. Alternative: embed permission claims in the JWT. Rejected: stale for up to the 30-minute token life and grows the token. Alternative: an in-memory cache invalidated on role change. Rejected: the per-request lookup is two small indexed queries on tables of a few dozen rows, so a cache adds failure modes for no measurable gain.

**4. System roles and the catalog are locked in the service layer and in the database.**
`RoleService` refuses any change touching an `IsSystem` role or permission. As defense in depth, SQLite triggers abort `UPDATE`/`DELETE` on system rows and on `RolePermissions` rows belonging to system roles. The Administrator grant set is reconciled at every startup so new catalog entries are granted to Administrator automatically; Manager and Employee grants are seeded once with the catalog and are not changed afterward. Alternative: service checks only. Rejected: the requirement says no one can change them, and a trigger covers direct database edits and future code paths. Trade-off: triggers complicate legitimate catalog upgrades, so the startup reconciler runs inside a migration step that temporarily has its own insert path (inserts are allowed; only update and delete are blocked).

**5. Catalog is defined in code as constants and seeded from them.**
A `Permissions` static class lists each key (for example `tasks.create`, `tasks.reassign`, `tasks.view.all`, `reports.view`, `workload.view`, `departments.manage`, `employees.manage`, `settings.reminders.manage`, `settings.application.manage`, `settings.ai.manage`, `dashboard.admin`, `dashboard.manager`, `assistant.use`). The initial seed matrix for Manager and Employee is derived from the current `[Authorize]` attributes so behavior is unchanged; building that table is an explicit task. `roles.manage` is flagged non-assignable: it is always held by Administrator and never offered for custom roles, enforced by the `AdminOnly` gate on the roles endpoints.

**6. Row-level scoping is driven by one permission, not the role name.**
`TaskAccessScope`, `TaskAccessHandler`, `WorkloadService` and assistant tools stop comparing role names and test `tasks.view.all` (organization-wide) versus its absence (own tasks only). `WorkloadService` currently lists users where `Role == Employee`; it will list active users whose role lacks `tasks.view.all`. Alternative: add a "base role" column to each custom role. Rejected: a second axis the admin would have to understand.

**7. One role per user; a role that is in use cannot be deleted.**
Deleting a role with users returns 409 with the count rather than silently reassigning, so nobody loses access by accident. The last active Administrator cannot be moved to another role.

**8. Migration with backfill; seeding on by default.**
One EF migration creates the three tables, inserts the system roles and catalog, adds `Users.RoleId`, backfills it from the old `Role` string, then drops `Role`. `Seed:Enabled` becomes `true`; `DatabaseSeeder` seeds the three users from `Seed` options (`Administrator`, `Manager`, `Employee` emails and passwords, matching `Login creds.txt`), creating only those that do not exist.

**9. SPA reads permissions from the profile endpoint.**
`/auth/me` returns role name and permission keys. `auth.guard` gains a `permissionGuard`; `nav-items` shows entries by permission. Home route: Administrator `/admin`, Manager `/manager`, any other role `/me`. Custom roles therefore land on the employee-style dashboard. Alternative: a configurable landing page per role. Deferred: not needed for the requested scope.

**10. Permissions Management screen uses a master-detail layout.**
Left: role list with a system/custom badge and a "New role" button. Right: checkbox matrix grouped by area (Tasks, Reports, Departments, Employees, Settings, Assistant). System roles render disabled; custom roles show Save and Delete. Placed at `settings/permissions`, guarded for Administrator.

## Risks / Trade-offs

- [Missed endpoint keeps a role-name check, leaving custom roles unable to use it or over-privileged] → A test enumerates all controller actions and fails if one carries a legacy role-name policy; the permission matrix task cross-checks each existing `[Authorize]`.
- [Triggers block a legitimate future catalog change] → Triggers block only update/delete of system rows; additions are inserts, and a removal would be a deliberate migration that drops and recreates the trigger.
- [Custom role gets tasks permissions but not `tasks.view.all`, so sees only own tasks] → Documented in the checkbox descriptions; the screen shows the description beside each permission.
- [Per-request permission lookup adds load] → Cache invalidated on role change; role tables are tiny.
- [Seeded credentials are public development defaults] → Acceptable for this project; documented as non-production in the proposal. Passwords are configuration, not code.
- [C:\ may not be writable for some Windows accounts] → Startup fails fast with a specific message and the root is configurable.
- [Existing signed-in sessions break after the migration because the role claim shape changes] → Role name claim is kept, and `SecurityStamp` is rotated for all users in the migration to force a clean re-login.
- [A role can be emptied of permissions and strand its users] → Allowed intentionally; users keep sign-in and profile access, which need no permission.

## Migration Plan

1. Ship the migration and seeder together; on a new machine the app creates `C:\TaskFlow` and everything in it.
2. On an existing developer database elsewhere, the migration backfills `RoleId`; no data is moved to `C:\TaskFlow`.
3. Rollback: stop the app, delete `C:\TaskFlow` (or point `Storage:Root` back) and use the previous build; the old `database/` folder is untouched.
