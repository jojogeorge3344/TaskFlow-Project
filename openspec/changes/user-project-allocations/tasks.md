## 1. Data model and migration

- [x] 1.1 Add `UserProjectAllocation` (with required `PositionId`), `Position` and `UserDepartment` entities, `AppDbContext` mappings (composite keys, cascades, unique case-insensitive position name, `AllocationPercent` check constraint 1 to 100) and navigation properties on `User`, `Project` and `Department`
- [x] 1.2 Add the EF migration: create the tables, seed positions (Member, Manager, QA, Developer, DevOps, System Admin), backfill from `Users.ProjectId` (100%, position Member) and `Users.DepartmentId`, then drop `ProjectId`, `DepartmentId`, `ManagerId`; write the `Down` path (done after the services stopped using the old columns, so the build stayed green)
- [x] 1.3 Check `DatabaseSeeder` and seed data for allocations and memberships (the seeder creates no users with a project, department or manager, so nothing needed changing; positions are seeded by the model and the migration)

## 2. Backend services

- [x] 2.1 Add `UserAllocationService` that replaces a user's allocations and department memberships, enforces whole percentages 1 to 100, a valid position and the 100% total (in a transaction on the project side), and returns total and remaining in its error
- [x] 2.2 Change `CreateUserRequest`, `UpdateUserRequest` and `UserDto` to `projectAllocations`, `departmentIds` and a derived `managers` list; remove `ManagerId` from `ManagerAssignmentRequest` (the percent, duplicate-project and position rules live in `UserAllocationService`, which every write path calls, so they return the same `{ error }` 400 everywhere)
- [x] 2.3 Implement the derived-managers query and use it in `UserService`, Profile and employee reads
- [x] 2.4 Update `ProjectService` and `DepartmentService` (member lists, counts, delete cascades, department members endpoint)
- [x] 2.5 Rewrite filters and scopes (`WorkloadService`, `ReportService`, `TaskService`, `DashboardTaskFilters`, admin/manager/employee analytics services and scopes, assistant tools) to match any allocation or membership, grouping each user once; return `allocatedPercent` in workload rows
- [x] 2.6 Add `PositionService` with `GET /positions` and `POST /positions` (case-insensitive get-or-create) and `PUT /projects/{id}/members` plus `GET /projects/{id}/members` (replace-all through `UserAllocationService`, all-or-nothing); the searchable employee list is the existing `GET /users?search=`, whose rows now carry `totalAllocatedPercent` and `remainingPercent`

## 3. Backend tests

- [x] 3.1 Add a test helper that seeds users with project allocations, departments and managers, and update the existing unit and integration tests that used `ProjectId`, `DepartmentId` or `ManagerId`
- [x] 3.2 Add unit tests for the cap (exactly 100, over 100, invalid percentages, duplicate project, two assigners one after the other), for derived managers, for positions (get-or-create ignoring case, position required, Manager position grants nothing) and for the migration backfill and downgrade. A truly parallel two-request race is not tested; the check and write share one transaction
- [x] 3.3 Add integration tests for create and update with allocations, the 400 cap response, filters matching any membership, project deletion, and `PUT /projects/{id}/members` (success, one row over the cap rejects all, position creation, permissions, remaining percent on the employee list)

## 4. Frontend

- [x] 4.1 Update `user.model.ts`, `user.service.ts`, department and workload models for allocations, departments and managers lists
- [x] 4.2 Build the project-allocation editor in the employee form (rows of project, position and percent, add and remove, running total and remaining, over-100 error blocks save) and the multi-select for departments
- [x] 4.3 Show departments, projects with percentages, total and managers in the employee list, Profile read-only fields, department members dialog and workload
- [x] 4.4 Update the filter bars, reports and dashboards so a project or department filter matches any membership
- [x] 4.5 Update and add frontend specs for the allocation editor, list, Profile, filters and the project team editor
- [x] 4.6 Add the skippable "Assign team" step to the project form dialog and the member editor to the project list: searchable employee dropdown showing remaining percent, position combobox (pick or type to create), percent input, over-100 handling

## 5. Verification

- [x] 5.1 Run all backend unit and integration tests and all frontend specs; confirm they pass
- [ ] 5.2 Run the migration on a copy of real data and check counts; check the employee form, project create then assign team, project member list, list, Profile, workload and dashboards in the browser
