# Proposal

## Why

TaskFlow keeps its SQLite database inside the solution folder and decides access by comparing a hard-coded three-value `Role` enum against role names scattered across controllers, route guards and the assistant. Only the administrator is seeded, so a fresh install has no manager or employee to sign in with, and there is no way for an administrator to define a new kind of user or change what a role may do. The application needs a stable per-machine data location, a seeded set of three accounts, and a real role/permission model that the administrator can manage.

## What Changes

- On startup, create `C:\TaskFlow\` if missing and place the SQLite database (`taskflow.db`), the AI key ring (`dp-keys\`) and mail attachments (`mail-attachments\`) under it. The root is configurable so tests and non-Windows hosts can override it.
- On first run, seed three accounts (Administrator, Manager, Employee) with their sign-in credentials, the three system roles, and the permission catalog with each system role's grants. Seeding is idempotent and enabled by default.
- Replace the `User.Role` enum with a `Roles` table and `User.RoleId`. Add `Permissions` and `RolePermissions` tables.
- Permissions form a **fixed catalog** defined in code and seeded as locked. Administrators cannot add, rename or delete permissions.
- The three system roles (Administrator, Manager, Employee) are **read-only**: their permission grants cannot be changed and the roles cannot be deleted, by anyone, through the API or the UI.
- Administrators can create, edit and delete **custom roles**, choosing each role's permissions from the catalog, and assign any user to one role.
- Authorization moves from role-name checks to permission checks, resolved from the database on each request so a change applies without re-login. Admin-only endpoints (role/permission management) stay tied to the Administrator system role.
- New **Settings > Permissions Management** screen, Administrator only: a role list plus a permission checkbox matrix. System roles render with every checkbox disabled; custom roles are editable and deletable.
- The SPA reads the signed-in user's permissions and uses them for route guards and menu items instead of role-name arrays.
- **BREAKING**: `User.Role` column is replaced by `RoleId`; API user payloads expose a role object/name instead of the enum; existing role-name `[Authorize]` policies are replaced.

## Capabilities

### New Capabilities
- `local-data-storage`: Per-machine data folder at `C:\TaskFlow`, database creation on first run, and the seeded Administrator, Manager and Employee accounts.
- `role-permissions`: Role and permission model, the locked permission catalog and system roles, custom role lifecycle, one-role-per-user assignment, and permission-based request authorization.
- `permissions-management-ui`: The Administrator-only Settings > Permissions Management screen with checkbox matrix, read-only system roles, and custom role create/edit/delete.

### Modified Capabilities
<!-- None: openspec/specs/ holds no capabilities yet; existing behavior lives in change artifacts. -->

## Impact

- **Backend**: `AppDbContext`, new entities and an EF migration (backfilling `RoleId` from the old `Role` column), `DatabasePathResolver`/storage options, `DatabaseSeeder`, `JwtTokenService`, `Program.cs` authorization policies, every controller using `AuthPolicies.AdminOnly`/`ManagerOrAdmin`, `TaskAccessScope`/`TaskAccessHandler`, `WorkloadService`, `UserService`, and the assistant authorizer and tools that read `Role`. New roles/permissions controller and service.
- **Frontend**: `auth.models`, `auth.guard`, `role-routes`, `nav-items`, `shell`, `app.routes`, employee management role picker, and a new permissions-management component under `features/settings`.
- **Config**: `appsettings.json` (`ConnectionStrings`, `Mail`, `AiProviders`, `Seed`) gains a storage root; `.gitignore` entries for `database/` become mostly historical.
- **Existing installs**: a fresh database is created at `C:\TaskFlow`; the old `database/` folder is left untouched and not migrated.
- **Security note**: seeded credentials are the well-known development accounts listed in `Login creds.txt`; they are acceptable for this project but must not be treated as production secrets.
