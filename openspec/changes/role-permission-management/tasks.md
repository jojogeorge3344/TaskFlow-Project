# Tasks

## 1. Storage root at C:\TaskFlow

- [x] 1.1 Add a `Storage:Root` option (default `C:\TaskFlow`) and a resolver that returns the database, `dp-keys` and `mail-attachments` paths; unit test default and override paths
- [x] 1.2 Create the root folder at startup before the DbContext is used and fail with a message naming the folder and cause when it cannot be created or written; unit test both outcomes against a temp folder
- [x] 1.3 Point `ConnectionStrings:Default`, `AiProviders:KeyRingPath` and `Mail:AttachmentsPath` at the resolver, update `appsettings*.json` and `TaskFlowApiFactory` to use a temp root, and verify the existing backend test suite still passes
- [x] 1.4 Verify by running the API once with the default root that `C:\TaskFlow\taskflow.db` exists and the old `database/` folder is untouched

## 2. Role and permission data model

- [x] 2.1 Add `Role`, `Permission` and `RolePermission` entities, replace `User.Role` with `RoleId`, and configure `AppDbContext` (unique case-insensitive role name, unique permission key)
- [x] 2.2 Define the permission catalog constants and build the seed matrix for Administrator, Manager and Employee by listing every existing `[Authorize]` policy and row-scope check; commit the matrix as a table in the catalog file's doc comment and verify each current endpoint maps to a key
- [x] 2.3 Create the EF migration: new tables, system roles and catalog inserts, `Users.RoleId` backfilled from the old `Role` string, old column dropped, `SecurityStamp` rotated; test the migration upgrades a database holding one user of each old role
- [x] 2.4 Add SQLite triggers that abort UPDATE/DELETE on system roles, system permissions and system roles' `RolePermissions`; test that raw SQL against those rows fails and inserts still work
- [x] 2.5 Add a startup reconciler that grants any catalog permission missing from Administrator and does not alter Manager or Employee grants; test with a newly added catalog key

## 3. Seeding

- [x] 3.1 Extend `SeedOptions` with Manager and Employee credentials, default `Seed:Enabled` to true, and seed the three accounts only when each is missing; test fresh DB creates three accounts with matching roles
- [x] 3.2 Test seeding is idempotent: a changed password or role on an existing seeded account is left unchanged, and passwords are stored only as hashes
- [x] 3.3 Update `Login creds.txt` references and the README to state the data folder and the three seeded sign-ins, and verify the documented credentials sign in against a fresh root

## 4. Permission-based authorization (backend)

- [x] 4.1 Add per-key permission policies and a `[RequirePermission]` attribute, and have session validation refresh the role and permission claims from the database on every request; integration test grant, deny and that claims reflect the database
- [x] 4.2 Switch `JwtTokenService` to the role-name claim from the `Roles` table and extend `/auth/me` and `AuthResponse` to return role name and permission keys; test the payload for each system role
- [x] 4.3 Replace `ManagerOrAdmin` and `AdminOnly` usage on every controller action with the permission policies from the 2.2 matrix, keeping `AdminOnly` only on roles and permissions endpoints; run the existing integration tests to confirm system-role behavior is unchanged
- [x] 4.4 Replace role-name checks in `TaskAccessScope`, `TaskAccessHandler`, `WorkloadService`, `UserService` and the assistant authorizer and tools with `tasks.view.all` and related permission checks; unit test an own-tasks-only custom role versus an org-wide one
- [x] 4.5 Add a test that enumerates controller actions and fails if any still uses a legacy role-name policy other than the roles endpoints
- [x] 4.6 Test that removing a permission from a custom role refuses that user's next request without re-login

## 5. Roles API

- [x] 5.1 Implement `RoleService` and `RolesController` (list roles with permissions, list catalog, create, rename, set permissions, delete) behind the Administrator-only gate; integration test each endpoint as Administrator and as Manager, Employee and a custom role
- [x] 5.2 Enforce rules: system roles reject update and delete, catalog write attempts are rejected, `roles.manage` cannot be granted, duplicate names rejected, role in use returns 409 with the user count; one test per rule
- [x] 5.3 Add role assignment to the user update endpoint, reject demoting the last active Administrator, and update `UserDto` to expose the role; integration test assigning a custom role and the last-admin guard

## 6. Frontend: auth and navigation

- [x] 6.1 Change `auth.models` to a role name string plus a permission key list, add a `permissionGuard`, and update `auth.service` to store permissions; unit test guard allow, deny and redirect
- [x] 6.2 Convert route guards in `app.routes.ts` and menu entries in `nav-items`/`shell` from role arrays to permissions, with home route Administrator `/admin`, Manager `/manager` and every other role `/me`; update `app.routes.spec`, `nav-items.spec` and `role-routes.spec`
- [x] 6.3 Replace the role dropdown in Employee Management with a list of all roles from the API; component test that system and custom roles appear and the selection is saved

## 7. Frontend: Permissions Management screen

- [x] 7.1 Add `settings/permissions` route and a Settings menu entry visible only to Administrators; test a non-administrator is redirected and does not see the entry
- [x] 7.2 Build the master-detail component: role list with system/custom badge and a checkbox matrix grouped by area; test checkbox state matches the role and system roles show every checkbox disabled with no Save or Delete
- [x] 7.3 Add create role, edit name and permissions with explicit Save, unsaved edits discarded on leave, and server errors shown while keeping edits; component tests for each
- [x] 7.4 Add delete with confirmation and the "role in use" message; component test both outcomes
- [x] 7.5 Run the frontend test suite and click through the screen in the running app as Administrator, Manager and a custom-role user, confirming the checkbox matrix, read-only system roles and that a permission change takes effect on the user's next request

## 8. Integration checks

- [x] 8.1 Start from an empty storage root and verify end to end: folder and database created, three accounts sign in and land on their home pages, system roles are read-only in the UI and API
- [x] 8.2 Create a custom role in the UI, assign it to a user, verify the user's access matches the ticked boxes, then remove the user from the role and delete the role
- [x] 8.3 Run `openspec validate role-permission-management --strict` and the full backend and frontend test suites

## Workflow follow-up

- Archive the change after the project's review requirements are satisfied.
