# Proposal

## Why

Today a user has exactly one project (`User.ProjectId`), one department (`User.DepartmentId`) and one manager (`User.ManagerId`). Real staff work across several departments and projects at once and report to more than one manager, and managers cannot see how much of someone's time a project takes. Planning needs a way to say "Priya is 60% on Apollo and 40% on Zephyr" without ever over-committing anyone beyond 100%.

## What Changes

- A user can belong to **many departments** (no percentage) and to **many projects**, each with an **allocation percentage**.
- When someone assigns a user to a project they enter the allocation percentage (whole number, 1 to 100). A user's allocations across all their projects must total **at most 100%**; the server rejects anything that would exceed it and the form shows the remaining percentage as it is edited.
- Each project assignment also carries a **position** on that project (for example Manager, QA, Developer, DevOps, System Admin), picked from a list or typed to create a new one.
- A project can be assigned its team right after it is created (a skippable "Assign team" step) and later from its member list: a searchable employee dropdown, then position, then percentage. It writes the same allocation rows as the employee form, under the same 100% cap.
- A user's **managers are derived**: they are the managers of the user's departments (the existing department-manager links). The single "Manager" field is replaced by a list.
- Existing single values are migrated: a user's current project becomes a 100% allocation, their current department becomes one department membership, and the single manager column is dropped (derived from departments from then on).
- Screens that assumed one project, department or manager show lists: employee form and list, Profile (read-only fields), Department members dialog, Workload, Reports, dashboards and task filters.
- **BREAKING** (API): `projectId`, `departmentId`, `managerId` on user create/update/read become `projectAllocations`, `departmentIds`, `managers`; filters by project or department now match any membership.

## Capabilities

### New Capabilities
- `user-project-allocation`: assigning a user to several projects with allocation percentages and the 100% cap.
- `user-department-membership`: a user belonging to several departments, and the manager list derived from them.
- `project-team-assignment`: assigning employees to a project from the project side, with a position and a percentage, including the post-create "Assign team" step and the reusable positions list.

### Modified Capabilities
<!-- None as main specs: the single project/department/manager behaviour is described in unarchived changes (department-management, admin-delete-manager-assignments-email-domain, departments-and-employees-impl) and is superseded here. -->

## Impact

- **Database**: new tables `UserProjectAllocations(UserId, ProjectId, AllocationPercent, PositionId)`, `Positions(Id, Name)` and `UserDepartments(UserId, DepartmentId)`, with a data-migrating EF migration; drop of `Users.ProjectId`, `Users.DepartmentId`, `Users.ManagerId`.
- **Backend**: `User` entity and `AppDbContext`, `UserService`, `ProjectService`, `DepartmentService`, `WorkloadService`, `ReportService`, `TaskService`, dashboard analytics filters and scopes (`DashboardTaskFilters`, `AdminAnalyticsService`, `ManagerAnalyticsService`, `ManagerDashboardScope`, `EmployeeAnalyticsService`), assistant action tools, seeders, validators and the many tests that seed users with a project or manager.
- **Backend (project side)**: `ProjectsController` gains a members endpoint, plus a positions endpoint and a searchable employee endpoint that returns remaining percentage.
- **Frontend**: `user.model.ts`, `user.service.ts`, employee form panel and list, project form dialog ("Assign team" step) and project member list, Profile, department members dialog, workload, reports and dashboard filters.
- **Related changes**: overlaps `admin-delete-manager-assignments-email-domain` (manager assignment) and `department-management`; archive order matters.
- Assumptions: whoever may edit a user today may edit their allocations; allocations are whole percentages; a user with no projects totals 0%; a position is a label only (a "Manager" position does not grant `ManagerProject` oversight); position names are unique ignoring case; deleting a project or department removes its allocation or membership rows.
