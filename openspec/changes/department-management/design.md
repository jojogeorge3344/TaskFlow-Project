# Design

## Context

The committed baseline has a single `Department` domain that the "Project Management" screen actually uses to manage projects: `User.DepartmentId` and `Task.DepartmentId` point at it, and `departments.view`/`departments.manage` guard its API. There is no real Project entity and no way to group people into departments. The working tree already contains the full implementation this design describes (backend, frontend, migration, tests) as uncommitted changes; this document records how it works and why. See proposal.md for motivation and `specs/` for the behavioral contract.

## Goals / Non-Goals

**Goals:**

- Rename the existing mislabelled department domain to a first-class Project domain without data loss.
- Introduce a real Department domain with CRUD and full-replacement member management, exposed through an API and a Department Management screen mirroring Project Management.
- Let employee creation/editing place a user in a department as well as a project.

**Non-Goals:**

- Department-scoped tasks, workloads, reports, or dashboards (tasks/projects keep project scoping only).
- Per-member incremental add/remove endpoints or department hierarchy/nesting.
- Changing the self-service profile (users still cannot change their own department/project there).
- Enforcing that department employees hold the Employee role (any active user may be assigned).

## Decisions

**Rename-in-place instead of a parallel new Project feature.** The old `Department` table, service, controller, and frontend feature are renamed to `Project` in one EF Core migration (`RenameDepartmentsToProjectsAndAddDepartments`): tables `Departments`→`Projects` and `ManagerDepartments`→`ManagerProjects` are renamed, columns `Users.DepartmentId`/`Tasks.DepartmentId`/`ManagerProjects.DepartmentId` become `*ProjectId`, and indexes/FKs follow. Alternative — keeping the old tables and adding Project as a copy — would have required dual writes and a backfill; renaming preserves all rows atomically. Down() reverses every step.

**Reused permission keys, new capability keys.** `departments.view`/`departments.manage` keep their database rows (a `TR_Permissions_NoUpdate` trigger blocks in-place updates, so the migration drops/recreates it only to refresh their display text from project wording to department wording). New `projects.view`/`projects.manage` rows are inserted by `AccessControlSeeder` on next startup; Administrator is re-granted every permission each start, Manager/Employee only at first insert, so upgrades give Manager `projects.view` automatically while existing grants are never overwritten.

**Departments own members through two mechanisms.** Managers attach via a `DepartmentManager` join table (composite PK `(ManagerUserId, DepartmentId)`, cascade deletes); employees attach via the single-valued `User.DepartmentId` FK (restrict). Employees therefore belong to at most one department, and assigning a user who is already in another department moves them. `PUT /departments/{id}/members` validates every id up front (managers: active + Manager role; employees: active, any role) and only then mutates, so one bad id rejects the whole replacement with `400` and no partial writes. Alternative — per-member add/remove endpoints — was rejected as it complicates the UI for no gain; the checkbox dialog naturally produces a full set.

**Soft delete with member guards.** Departments and projects both use `IsDeleted` with global query filters. Department delete is blocked (`400`, not `409`, matching the app's existing error style) while any manager link or assigned user remains; project delete is blocked while assigned users remain. Both service-level uniqueness checks ignore soft-deleted rows, but the DB unique index on `Name` is not filtered — reusing a deleted name surfaces as an unhandled `DbUpdateException` (known trade-off, documented in risks).

**Frontend mirrors Project Management.** `features/departments` (new) and `features/projects` (git-renamed from the old `features/departments`) share the card-list + dialog pattern: `DepartmentListComponent` shows both manager and employee counts, `DepartmentMembersDialogComponent` loads members and both role-filtered pickers in parallel (`GET /users?role=Manager&isActive=true`, `role=Employee`) and submits the full checked sets, and `EmployeeFormPanelComponent` gains a `#emp-department` select fed by `departmentService.getAll()`. The departments route and nav item are gated on `departments.manage` (the screen itself has no in-component checks); projects stays on `projects.view` with `projects.manage`/`employees.manage` gating individual controls.

**DI and contracts.** `IDepartmentService`/`DepartmentService` and `IProjectService`/`ProjectService` are registered scoped in `Program.cs`; FluentValidators for department create/update are registered by assembly scan and run in the controller (project validators exist but the project controller does not run them — a pre-existing gap left as-is to avoid behavior drift beyond this change's scope).

## Risks / Trade-offs

- [Soft-deleted names remain in the unique index → recreating a deleted name fails with an unhandled 500] → Acceptable at current scale; a filtered unique index or name suffixing would fix it in a follow-up.
- [Inactive manager links survive and are counted in `managerCount` but cannot be re-selected in the members dialog (only active Manager-role users are pickable); inactive current members are surfaced with an Inactive badge and can only be removed] → Intentional: the dialog never silently drops existing members.
- [Manager soft-deletion does not remove `DepartmentManager` rows, so counts can exceed the rendered member list (which excludes soft-deleted users via the global filter)] → Follow-up cleanup task if it becomes visible in practice.
- [Reused `departments.*` permission keys change meaning on upgraded databases (old grants now protect the new department feature)] → Grants happen to match (Administrator all, Manager view), and the migration rewrites the display text; any custom role grants carry over, which is the least surprising outcome.
- [The project controller does not invoke the existing project validators, so blank/overlong project names are not API-validated] → Out of scope for this change; noted for a future hardening pass.
- [`GET /projects/{id}/employees` returns raw `User` entities, serializing sensitive fields] → Pre-existing exposure preserved under the rename; deserves a DTO follow-up.

## Migration Plan

1. Deploy the API; startup runs `Database.MigrateAsync()` which applies `RenameDepartmentsToProjectsAndAddDepartments` (drops old FKs, renames tables/columns/indexes, creates `Departments`/`DepartmentManagers`, adds `Users.DepartmentId`, re-adds FKs, refreshes `departments.*` permission text) — all data preserved in place.
2. `AccessControlSeeder` inserts `projects.view`/`projects.manage` and grants them (Administrator both, Manager `projects.view`).
3. Frontend ships with the rename and the new `/departments` screen; no client-side data migration needed.
4. Rollback: redeploy the previous build and run the migration's `Down()`, which reverses every rename and drops the new tables/column.

## Open Questions

- None blocking. Whether project validators should be wired into `ProjectsController` (and whether a project-employees DTO should replace raw `User` serialization) can be decided in a separate hardening change without affecting these specs or tasks.
