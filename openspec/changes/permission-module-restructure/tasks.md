# Tasks

## 1. Catalogue regrouping

- [x] 1.1 In `PermissionCatalog.cs`, move every definition to its new module, screen and section per `specs/permission-module-hierarchy/spec.md` (Task Management, Employee Workload, Board, TaskFlow Management submodules, Communication Management, Report, Settings submodules). Verify with a unit test that lists each module once and that every key appears exactly once.
- [x] 1.2 Rename the displayed "Tasks" module to Task Management and keep Dashboard and TFA assistant as separate modules. Verify with a test that no module is named "Tasks" and that Dashboard and TFA assistant exist.
- [x] 1.3 Add a test comparing the full key list before and after the regrouping (a stored copy of the old list) to prove no key was renamed or removed.
- [x] 1.4 Check the Role Configuration tree and matrix render the new grouping and update their component specs. Verify with the existing `permission-tree` and `permissions-management` specs passing and a new case for the module order.

## 2. Board and Workload permissions (backend)

- [x] 2.1 Add `board.view`, `board.filters.use`, `workload.filters.use` and `workload.tasks.open` to `PermissionKeys.cs` and `PermissionCatalog.cs` with the seeding flags from design decision 4. Verify with the catalogue integrity test.
- [x] 2.2 Require `board.view` on `GET /tasks/board` in `TasksController`. Verify with an integration test: 403 without the key, 200 with it, and own-versus-all task scope unchanged.
- [x] 2.3 Verify seeding in a test that starts from a database made before the change: Manager, Employee, a pre-existing custom role and the Administrator all hold the Board keys; workload view holders hold both new workload keys.
- [x] 2.4 Mirror the four keys in `permissions.ts` and run the existing backend and frontend key-list comparison test.

## 3. Board and Workload permissions (frontend)

- [x] 3.1 Guard `/board` with `permissionGuard` for `board.view`, and show the Board menu entry in `nav-items.ts` only with it. Verify with route guard and nav specs, including a user holding only Board view.
- [x] 3.2 Hide the Board filter controls without `board.filters.use`. Verify with a Kanban component spec for both cases.
- [x] 3.3 Hide the workload filters without `workload.filters.use` and the employee tasks link without `workload.tasks.open`. Verify with workload component specs.
- [x] 3.4 Guard `/tasks/:id/activity` with `tasks.activity.view` and hide its links without it. Verify with a route spec for the direct-URL case.

## 4. Role-escalation safeguards

- [x] 4.1 Make `roles.manage` assignable again and add the Administrator-only, non-assignable permission `roles.administrator.assign` (backend and frontend key lists). Verify with the catalogue tests and the key-list comparison test.
- [x] 4.2 Remove the unreleased migration `RemoveRoleManagementFromCustomRoles` and its test, because roles that hold `roles.manage` keep it. Verify the migration tests pass and no migration file remains.
- [x] 4.3 In `UsersController` create and update, and the role list endpoint, use `roles.administrator.assign` instead of `roles.manage` for the Administrator role. Verify with integration tests for the scenarios in `specs/role-assignment-safeguards/spec.md`.
- [x] 4.4 Stop the employee update from applying `Password`. Verify with an integration test that the other fields save and the password hash is unchanged.
- [x] 4.5 Add the roles list for holders of `employees.role.assign` and use it in the employee form and role filter. Verify with an integration test (200 for assign-only, 403 without either key) and a form spec showing no error hint.
- [x] 4.6 In `RolesController`, refuse create and update from a caller without `roles.administrator.assign` when the keys include `roles.manage`, `employees.role.assign` or `roles.administrator.assign`, or when the role being edited already holds one. Verify with integration tests for a delegate (refused) and an Administrator (accepted), and that a delegate can still create an ordinary role.
- [x] 4.7 In Role Configuration, show the three restricted permissions as Administrator only and unticked for a user without `roles.administrator.assign`. Verify with a component spec for an Administrator and for a delegate.

## 5. Cleanup and integration

- [x] 5.1 Fix the stale comments listed in `Artifacts/permission-audit/permission-gaps.md` section F (`permissions.ts`, `SettingsController`, `tfa-settings.component.ts`, employee form hint). Verify by searching for the old wording.
- [ ] 5.2 Run the full backend and frontend test suites and verify they pass.
- [ ] 5.3 Sign in as Administrator, Manager, Employee and a new custom role in the running app and verify the menu, Board, Workload, Role Configuration grouping and 403 behaviors match the specs.
- [x] 5.4 Update `Artifacts/permission-audit/` rows for the changed permissions (inventory, matrix) and verify the key counts match the catalogue.

## Workflow follow-up

- Archive the change after review and verify the archived specs.
- Check the live database for custom roles holding `roles.manage` before running the migration (it removes the grant without logging).
