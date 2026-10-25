## Context

`User` has single `ProjectId`, `DepartmentId` and `ManagerId`. `DepartmentManager` links managers to departments (many to many) and `ManagerProject` links managers to projects. About 30 files reference `ProjectId`, including `WorkloadService`, `ReportService`, `ProjectService`, `DashboardTaskFilters`, the admin/manager/employee analytics services and scopes, assistant tools and seeders. The frontend uses single `projectId`, `departmentId` and `managerId` in the employee form and list, Profile, department members dialog, workload, reports and dashboard filters. Migrations are EF Core (`TaskFlow.Infrastructure/Migrations`).

## Goals / Non-Goals

**Goals:** many departments, many projects with percentage, many derived managers, 100% cap enforced on the server, data-preserving migration.
**Non-Goals:** time tracking, capacity planning by hours, changing which projects a manager oversees (`ManagerProject` stays), changing task assignment rules, a percentage on departments.

## Decisions

1. **Tables.** `UserProjectAllocations(UserId, ProjectId, AllocationPercent, PositionId)` with composite key `(UserId, ProjectId)`, a required foreign key to `Positions`, and a check constraint `AllocationPercent BETWEEN 1 AND 100`; `Positions(Id, Name)` with a unique case-insensitive name, seeded with Member, Manager, QA, Developer, DevOps and System Admin; `UserDepartments(UserId, DepartmentId)` with composite key. The allocation and department tables cascade-delete from user, project and department respectively; deleting a position that is in use is refused.
2. **100% cap.** Enforced in one `UserAllocationService` used by every write path (user create/update, project member assignment, assistant tools). It loads the user's allocations, applies the requested set, and refuses when the sum exceeds 100. Writes run in a serializable transaction (or with a row version on the user) so two concurrent assigners cannot both pass the check. A DB check constraint on the per-row range backs it up; the sum cannot be a simple constraint.
3. **Request shape.** User create/update take `projectAllocations: [{ projectId, percent, positionId }]` (replace-all semantics, like the existing manager assignment) and `departmentIds: [int]`. The cap error is a 400 with the total and remaining percentage.
4. **Managers derived.** A query helper returns `DepartmentManager.ManagerUserId` for the user's departments, distinct, excluding the user. `ManagerAssignmentRequest.ManagerId` is removed; `ManagerProjectIds` for managers stays.
5. **Migration.** One EF migration: create the tables, seed `Positions`, backfill `UserProjectAllocations` (100%, position Member) from `Users.ProjectId` and `UserDepartments` from `Users.DepartmentId` for non-deleted users, then drop `ProjectId`, `DepartmentId` and `ManagerId` (and their indexes and foreign keys). The `Down` migration restores the columns from the highest allocation and first department.
6. **Query rewrites.** Filters that used `user.ProjectId == x` become `user.ProjectAllocations.Any(a => a.ProjectId == x)`; `DepartmentId` likewise through `UserDepartments`. Workload and reports group per user once even when several memberships match. Workload rows also return `allocatedPercent`.
7. **Frontend.** The employee form gets a multi-select for departments and a project-allocation editor (rows of project select plus percent input, add and remove row, running total, remaining, over-100 error that disables save). The list and Profile show chips with percentages and the manager list. Filter bars keep their single-value pickers but match any membership.
8. **Project-side assignment.** `PUT /projects/{id}/members` takes `[{ userId, positionId, percent }]` with replace-all semantics for that project's members, and goes through `UserAllocationService`, so the 100% cap is checked per affected user exactly as from the employee form (one user over the cap rejects the whole request, nothing saved). `GET /positions` lists positions; `POST /positions` creates one from typed text and returns the existing one when the name already exists ignoring case. A searchable employee endpoint returns each employee with `allocatedPercent` and `remainingPercent`.
9. **Project-side frontend.** The project form dialog gets a skippable "Assign team" step after creation: a searchable employee dropdown that shows remaining percentage, a position combobox (pick or type to create), and a percent input per row. The same row editor is used from the project member list for later edits. A "Manager" position is a label only and grants no `ManagerProject` oversight.
10. **Rollout order.** Backend model and migration first, then services and queries, then the DTO change, then frontend, so every step keeps the build green. Because the contract is breaking, the API and frontend ship together.

## Risks / Trade-offs

- **Blast radius**: many services and about 60 tests seed users with a project or manager; a test helper that takes project/department/manager arguments and creates the new rows keeps those edits mechanical.
- **Existing manager data lost**: a user's stored single manager is dropped; users whose manager is not a manager of their department lose that link. Mitigation: migration logs affected user counts; confirm with the owner before running on real data.
- **Percentages and history**: allocation changes are not versioned, so past reports use current allocations.
- **Overlap** with unarchived changes `department-management` and `admin-delete-manager-assignments-email-domain`; archive them first or reconcile on archive.

## Migration Plan

Back up the database, run the migration, verify counts (users with project equals users with an allocation row; users with department equals memberships), then deploy backend and frontend together. Rollback: apply the `Down` migration and redeploy the previous build.

## Open Questions

- Which permission gates editing allocations (assumed the existing one for editing users)?
- Should managers be able to see, but not edit, allocations of their team?
- Who may create new positions? Assumed anyone who can assign project members.
- Should a project be required to have a Manager position? Assumed no.
