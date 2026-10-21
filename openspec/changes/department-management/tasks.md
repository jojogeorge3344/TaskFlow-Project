# Tasks

## 1. Domain rename: Department → Project

- [x] 1.1 Rename the domain entities: `Department` → `Project`, `ManagerDepartment` → `ManagerProject` (join now references ProjectId), delete the old entity files — verify `TaskFlow.Domain` compiles with no `ManagerDepartment` references left
- [x] 1.2 Rename `User.DepartmentId` → `User.ProjectId` and `Task.DepartmentId` → `Task.ProjectId` with their navigations — verify entity references compile across Domain
- [x] 1.3 Add the new `Department` and `DepartmentManager` entities (manager join, employee link via `User.DepartmentId`) — verify both exist with the shapes in design.md
- [x] 1.4 Update `AppDbContext`: DbSets and model config for `Projects`, `ManagerProjects`, `Departments`, `DepartmentManagers`, renamed FKs/indexes, global query filters for Projects and Departments — verify configuration matches the snapshot
- [x] 1.5 Add EF Core migration `RenameDepartmentsToProjectsAndAddDepartments` (table/column/index renames, new tables, `Users.DepartmentId`, permission display-text refresh with trigger drop/recreate) — verify `Up()`/`Down()` cover every rename and the snapshot is regenerated

## 2. Permissions and application-layer rename

- [x] 2.1 Add `projects.view`/`projects.manage` to `PermissionKeys` and `PermissionCatalog` (Manager gets view on first insert, Administrator both) — verify catalog entries render in the role-permission UI data and seeder grants on startup
- [x] 2.2 Rename the Application `Departments` folder/contracts to `Projects` (`ProjectDto`, `ProjectWithCountDto`, create/update requests, `IProjectService`, validators) — verify no stale department-project references compile
- [x] 2.3 Rename workload/report/dashboard/user DTO fields `department*` → `project*` (including the report CSV "Department" column → "Project") — verify unit tests for ReportCsv/Workload/Dashboard pass

## 3. Projects backend

- [x] 3.1 Implement `ProjectService` (list with employee counts, unique trimmed names, soft-delete blocked while users are assigned, manager-link cleanup on delete) — verify service unit coverage for counts, duplicates, and the delete guard
- [x] 3.2 Implement `ProjectsController` (`api/v1/projects`: list, by-id, employees, create, update, delete) under `projects.view`/`projects.manage` — verify duplicate-name 400 and delete-guard 400 behaviors
- [x] 3.3 Point tasks, workload, reports, dashboards, and assistant project lookups at the renamed project entity/fields — verify existing suites still pass with the rename

## 4. Departments backend

- [x] 4.1 Add Departments contracts (`DepartmentDto`, `DepartmentWithCountsDto`, `DepartmentMembersDto`, `MemberRefDto`, create/update/replace-members requests) and FluentValidators (name 1–200, description ≤500) — verify validator unit tests cover blank/overlong boundaries
- [x] 4.2 Implement `DepartmentService` (list with manager/employee counts, case-insensitive unique names, soft-delete blocked while members exist, `GetMembersAsync`, atomic `ReplaceMembersAsync` with active/role validation) — verify unit tests for duplicate names, member replacement, and rejection of inactive/wrong-role managers
- [x] 4.3 Implement `DepartmentsController` (`api/v1/departments` + `/{id}/members`) under `departments.view`/`departments.manage` with 201+Location, 400 error payloads, 404s, and 204 delete — verify integration tests in `DepartmentsEndpointsTests` pass (CRUD, duplicate 400, member replace/clear, delete blocked while members remain, Manager read-only, Employee 403, 401 unauthenticated)
- [x] 4.4 Register `IDepartmentService` in `Program.cs` DI and confirm `AccessControlSeeder` rewrites `departments.*` display text via the migration — verify startup seeds without trigger errors

## 5. User department/project assignment

- [x] 5.1 Add `DepartmentId` to `CreateUserRequest`/`UpdateUserRequest` and validate it exists and is not soft-deleted (`Department does not exist.`); project id validation kept in sync — verify service tests for unknown ids and for clearing with null
- [x] 5.2 Expose `DepartmentId`/`DepartmentName` on `UserDto` projections — verify user list/by-id payloads carry the department fields

## 6. Frontend rename and Project Management screen

- [x] 6.1 Rename `features/departments` → `features/projects` with `ProjectService`/project models and the `/projects` route (guard `projects.view`, in-screen `projects.manage`/`employees.manage` gating) — verify `project-list`/`project-form-dialog` specs and `app.routes.spec` pass
- [x] 6.2 Update nav ("Project Management" under TaskFlow Management), `permissions.ts` keys, and rename department* → project* across workload, reports, profile, and dashboard UI — verify `nav-items.spec` grouping and wording-dependent specs pass

## 7. Frontend Department Management screen

- [x] 7.1 Add `DepartmentService` and department models (getAll/getById/create/update/remove/getMembers/replaceMembers) — verify `department.service.spec` asserts every endpoint and the replace-members body
- [x] 7.2 Build `DepartmentListComponent` (cards with manager/employee counts, name search, Add/Edit dialogs, delete guard while members exist, loading/empty/no-match/error states) — verify `department-list.component.spec` covers counts, search, delete guard, and retry
- [x] 7.3 Build `DepartmentFormDialogComponent` (name required, description optional, server-error display, add/edit toasts) — verify `department-form-dialog.component.spec` covers validation, duplicate-error persistence, and successful edit
- [x] 7.4 Build `DepartmentMembersDialogComponent` (parallel load of members + active Manager/Employee pickers, pre-checked sets, Inactive badges, full-set replace on save, error handling) — verify `department-members-dialog.component.spec` covers pre-check, inactive retention, full-set save payload, and load/save failures
- [x] 7.5 Wire the `/departments` route and nav entry under `departments.manage` — verify `app.routes.spec` and `nav-items.spec` assertions
- [x] 7.6 Add the Department select to the employee create/edit form and department display on the details panel — verify `employee-form-panel.component.spec` includes departmentId in create/update payloads and the Unassigned flow

## 8. Verification

- [x] 8.1 Run the backend test suite (`dotnet test`) and confirm all unit and integration tests pass with no skipped regressions
- [x] 8.2 Run the frontend unit tests (project test runner) and confirm all specs pass
- [x] 8.3 Apply the migration to a database copy containing existing department-named data and confirm rows arrive as projects with users/tasks intact

## Workflow follow-up

- Archive the change after the project's review requirements are satisfied.
- Verify the archived result.
