# Tasks

## 1. Catalogue model and inventory

- [x] 1.1 Extend `PermissionDefinition` with Module, Screen, Section, Action, ParentKey, ScopeGroup, SortOrder and required-View dependency; verify with a unit test that the record compiles and a sample entry round-trips.
- [x] 1.2 Author the full module tree in `PermissionCatalog` (Dashboard, Tasks, Projects, Departments, Employees, Reports & Workload, Mail, Notifications, AI Assistant, Settings, Role Configuration, Profile) with child keys, scope groups and default Manager/Employee grants matching today's effective access; verify with a catalogue-integrity unit test (unique keys, every parent exists, each module once, no per-role sections).
- [x] 1.3 Add `PermissionKeys` constants for every new key and mirror them in `frontend/src/app/core/auth/permissions.ts`; verify with a test that the two key sets are identical.
- [x] 1.4 Add an integrity test that every `[RequirePermission]` key and every controller action is either keyed or on an explicit open-endpoint allow-list; verify the test fails when a keyed action is removed.

## 2. Persistence, seeding and migration

- [x] 2.1 Keep catalogue hierarchy metadata in code (the database makes `Permissions` rows read-only, so no schema change); verify the existing migrations still apply cleanly on an empty database and the seeder runs on top of them.
- [x] 2.2 Update `AccessControlSeeder` to insert new permissions and metadata, keep Administrator = every assignable permission, and run the one-time parent-to-child grant expansion keyed on the catalogue version; verify with an integration test that re-running the seeder is idempotent.
- [x] 2.3 Write a migration snapshot test: for Administrator, Manager, Employee and a seeded custom role, effective permissions on every existing endpoint are identical before and after (no loss, no widening); verify it passes.
- [x] 2.4 Resolve effective permissions as the union of explicit grants plus scope implications (higher scope implies lower) and expose them in the auth claims and `/auth/me`; verify with unit tests for scope implication and an integration test of the claims.

## 3. Roles API

- [x] 3.1 Return the catalogue as a tree-ready DTO (module, screen, section, action, parent, scope group, assignable, locked) from `GET permissions`; verify with an integration test on shape and ordering.
- [x] 3.2 Validate saves: unknown keys, non-assignable keys, child without required View, and custom roles never receiving Role Configuration permissions; verify with integration tests for each rejection message.
- [x] 3.3 Keep system roles read-only and add a "copy from role" create path that never mutates the source; verify with integration tests that Manager is unchanged after duplicating and editing a copy.

## 4. Backend enforcement

- [x] 4.1 Gate Tasks (list scope, board, details, create, copy, edit, delete, status, reassign, recipients, comments, activity) with child keys, giving delete its own `tasks.delete`; verify with an API test per action returning 403 for a role lacking the key and 200 with it, and that Own scope still limits rows.
- [x] 4.2 Gate Projects, Positions and Departments by action (view, create, edit, delete, manage members); verify 403/200 integration tests for each.
- [x] 4.3 Gate Employees by action (view, details, create, edit, activate/deactivate, delete with `employees.delete`, assign role, reset MFA); verify 403/200 tests and that no endpoint still uses `roles.manage` for deletes.
- [x] 4.4 Gate Dashboard by scope and section/filter (own, team, organization) on the existing endpoints and responses; verify a Team-scope role is refused organization data and an Organization-scope role can read all three.
- [x] 4.5 Gate Reports (view, export), Workload, Settings (reminders, application, AI providers: view/edit/test/remove model) and Role Configuration actions; verify 403/200 tests.
- [x] 4.6 Register Mail, Notifications, AI Assistant and Profile/MFA features in the catalogue as locked-on self-service and keep their endpoints open to signed-in users; verify existing tests still pass and the allow-list test covers them.
- [x] 4.7 Make assistant tools respect the new child keys; verify with a unit test that a role lacking `tasks.create` cannot create a task through the assistant.

## 5. Frontend enforcement

- [x] 5.1 Keep the existing `AuthService.hasPermission` helper (screens wrap it in computed signals, so no structural directive is added) and make `dashboardRouteFor` scope-aware for custom roles; verify with nav and guard specs.
- [x] 5.2 Update `app.routes.ts` guards to the new View/child keys and fix the `/departments` and `/employees` drift; verify with `app.routes.spec.ts` that each route redirects a role without its key and requests no data.
- [x] 5.3 Update `buildNavigation()` for the new keys; verify `nav-items.spec.ts` for the three system roles (menus unchanged) and for a custom role with a minimal set.
- [x] 5.4 Gate buttons, tabs and widgets on Tasks, Task details, Projects, Departments, Employees, Reports, the dashboard filter bar and Settings; verify with component specs that controls are absent without the key. (Individual dashboard chart widgets are not hidden separately: "View charts and reports" is enforced on the analytics endpoints.)

## 6. Role Configuration screen

- [x] 6.1 Build the tree view-model (module, screen, feature nodes with tri-state, counts, scope selectors, dependency auto-enable) and `role.model`/`role.service` changes; verify with unit specs for select-all, module toggle, partial state and dependency rules.
- [x] 6.2 Rebuild `permissions-management` template and styles: role rail, module navigator with counts, tree panel, search, Enabled/Disabled filter, expand/collapse, Select all/Deselect all, sticky save bar, unsaved-changes confirm; verify with component specs for each control.
- [x] 6.3 Add system-role read-only view with Duplicate-as-custom and Copy-from-role on new role; verify specs that system roles show disabled controls and the source role is unchanged.
- [x] 6.4 Add the optional role-by-permission matrix view; verify the matrix shows the same grants as the tree for each role.
- [ ] 6.5 Make the layout responsive (rail plus tree on wide, drill-in on narrow, no horizontal scroll); verify with a layout spec at phone and desktop widths and a manual browser check.

## 7. Final verification

- [ ] 7.1 Cross-check that every actual feature (endpoint, route, tab, widget) appears under its correct module and that no module is duplicated, using the integrity tests plus a manual walkthrough of each screen; verify the checklist in the PR.
- [ ] 7.2 Sign in as Administrator, Manager, Employee and a custom role and exercise each module in the browser and via direct API calls; verify unauthorized UI is hidden, URLs are blocked and API returns 403.
- [ ] 7.3 Run the full backend and frontend test suites; verify they pass.
