# Proposal

## Why

The app's "Project Management" screen has been managing rows in the `Department` table all along — there is no real Project entity, and no way to organise people into actual departments. Administrators need a Department Management screen, mirroring Project Management, to group managers and employees into departments, and employee creation needs a way to place a user in a department as well as a project.

## What Changes

- **BREAKING** (internal domain/API rename): the existing `Department` entity, table, service, controller, permissions, and frontend service/models are renamed to `Project` (`api/v1/departments` → `api/v1/projects`, `User.DepartmentId` → `User.ProjectId`, `Task.DepartmentId` → `Task.ProjectId`, manager↔department join → manager↔project). Data is preserved by an EF Core migration that renames tables and columns in place.
- New first-class `Department` entity/table with CRUD API (`api/v1/departments`) and a members API (`GET`/`PUT api/v1/departments/{id}/members`) under the existing `departments.view` / `departments.manage` permission keys (reused, now guarding real departments; `projects.view` / `projects.manage` added for the renamed Project feature).
- New Department Management screen (route `/departments`, nav "Department Management") with the same card layout as Project Management: name, description, manager and employee counts, plus a **Manage members** dialog that replaces a department's managers (active Manager-role users) and employees (active users, applied as the employee's `DepartmentId`). Deletion is blocked while any managers or employees remain.
- Employee create/edit form gains a **Department** select alongside the existing **Project** select; both are sent in the user create/update payloads.
- Workload, reports, dashboards, profile, user list filter, and assistant surfaces rename `department*` → `project*` consistently (report CSV column "Department" → "Project"; help topic for departments now points at `/departments`).
- EF Core migration covering the rename plus the new `Departments` and `DepartmentManagers` tables, `Users.DepartmentId` column, and permission display-text refresh for `departments.*`.

## Capabilities

### New Capabilities

- `project-management`: Project CRUD for Administrators (list with employee counts, unique names, soft-delete guarded by assigned employees); the previously mislabelled department behaviour now owned by a real Project entity.
- `department-management`: Department CRUD plus member management for Administrators — replacing managers and employees per department, manager/employee counts on the list, delete blocked while members remain — and placing users in a department from the employee form.

### Modified Capabilities

<!-- none: openspec/specs/ is empty; no archived capabilities exist to modify. -->

## Impact

- **Backend**: `TaskFlow.Domain` (new `Project`, `ManagerProject`, `Department`, `DepartmentManager` entities; `ManagerDepartment` removed; `User`/`Task` FK renames), `TaskFlow.Application` (folder `Departments` → `Projects`, new `Departments` contracts, User/Workload/Report/Dashboard DTO renames), `TaskFlow.Infrastructure` (`ProjectService`, new `DepartmentService`, `AppDbContext`, migration `RenameDepartmentsToProjectsAndAddDepartments`, `AccessControlSeeder` grants), `TaskFlow.Api` (`ProjectsController`, new `DepartmentsController`, `Program.cs` DI), `PermissionKeys`/`PermissionCatalog` (+`projects.view`/`projects.manage`).
- **Frontend**: `features/departments` → `features/projects`, new `features/departments` (list + form dialog + members dialog), `core/models`/`core/services` for both, `app.routes.ts`, `nav-items.ts`, `permissions.ts`, employee form/details department select, employee list input, workload/reports/profile field renames.
- **Tests**: backend integration (`DepartmentsEndpointsTests`) and unit (`DepartmentRequestValidatorsTests`) suites for departments; updated existing suites for renames; frontend specs for department list/form/members dialog, service, routes, and nav.
- **Data**: SQLite migration renames `Departments`→`Projects`, `ManagerDepartments`→`ManagerProjects`, renames FK columns on `Users`/`Tasks`; creates new `Departments`/`DepartmentManagers` tables; existing rows preserved.
