# Tasks

## 1. Email domain policy (backend)

- [x] 1.1 Add an `EmailPolicy` (domain constant `taskflow.com`, normalize, local-part validation) in the Application layer; verify unit tests cover case folding, other domains, empty/overlong/invalid local parts and consecutive dots
- [x] 1.2 Apply the policy in `CreateUserRequestValidator`, `UpdateUserRequestValidator` and `UpdateOwnProfileRequestValidator`; verify validator tests reject `a@gmail.com` and accept `a@taskflow.com`
- [x] 1.3 Make `UserService` create, update and own-profile uniqueness checks compare the local part case-insensitively against all users including deleted ones; verify tests for same email, same local part, own email unchanged, and a deleted user's email
- [x] 1.4 Switch the seeded accounts in `DatabaseSeeder` to `@taskflow.com` and update `Login creds.txt`, README and any test fixtures using `@taskflow.local`; verify `dotnet test` passes with no `taskflow.local` left (grep)

## 2. Email migration

- [x] 2.1 Add an EF migration that rewrites existing emails to `<local>@taskflow.com` in creation order with numeric suffixes on collisions, then adds a unique NOCASE index on `Email`; verify against a copy of a database containing `jo@gmail.com`, `jo@yahoo.com` and the `.local` seeds that results are `jo`, `jo2` and the three `@taskflow.com` seeds
- [x] 2.2 Confirm sign-in by converted email and by username with unchanged passwords; verify an integration test or manual login for admin, manager and employee seeds
- [x] 2.3 Document the breaking change and the backup advice in the README; verify the note is present

## 3. Email UI

- [x] 3.1 Add a shared domain constant and a local-part input with static `@taskflow.com` suffix (strip text after `@`, lowercase, trim) for the employee form; verify component tests for typing, pasting a full address and submitting `<local>@taskflow.com`
- [x] 3.2 Use the same input on the profile screen and show the server's duplicate-email error; verify component tests

## 4. Soft delete for employees

- [x] 4.1 Add `UserService.DeleteAsync` (self and last-admin guards, reject when unfinished assigned tasks exist, set `IsDeleted`/`IsActive`, end sessions, clear reports' `ManagerId` and manager-department rows) in one transaction; verify unit tests for each rule and for history retention
- [x] 4.2 Add `DELETE /api/users/{id}` restricted to the Administrator role (204/400/403/404); verify controller or integration tests, including a Manager holding `employees.manage` getting 403
- [x] 4.3 Verify login and token refresh reject deleted users and that lists, assignee/recipient pickers, department members and workload exclude them while tasks, comments and mail keep their names; add tests and fix any query that drops history rows
- [x] 4.4 Add the Delete action with confirmation to the employees screen for Administrators only, showing server errors; verify component tests for visibility by role, cancel, and error display

## 5. Soft delete for tasks

- [x] 5.1 Add `TaskService.DeleteAsync` (set `IsDeleted`, write an activity entry with actor and time); verify unit tests for success, not found and already deleted
- [x] 5.2 Add `DELETE /api/tasks/{id}` restricted to the Administrator role; verify 204/403/404 tests, including a Manager holding `tasks.update` getting 403
- [x] 5.3 Verify deleted tasks are excluded from list, board, direct fetch, reports/export, workload, dashboards, reminders, notifications opening and assistant tools; add a test per area and fix any gap
- [x] 5.4 Add the Delete action with confirmation to task details and the task list for Administrators only, navigating away on success; verify component tests for visibility by role and cancel

## 6. Manager assignments (data)

- [x] 6.1 Add `User.ManagerId` (self-FK, set null on delete) and a `ManagerDepartment` join entity, configure them in `AppDbContext`, and add an EF migration; verify the migration applies to a fresh and an existing database
- [x] 6.2 Add `PUT users/{id}/assignments` (Administrator-only) and extend `UserDto` with manager, department and report fields; enforce active-Manager target, no self-management or cycles, departments exist, and manager-only department assignment; verify unit tests for each rule and for replace-the-set, and integration tests for the 403 cases
- [x] 6.3 Clear manager links and department assignments when a manager's role changes or a department is deleted; verify tests
- [x] 6.4 Add Manager select (non-managers) and Departments multi-select (managers) to the employee form and show them in the details panel and own profile; verify component tests and that non-admins cannot edit them
- [x] 6.5 Verify manager task, workload, report and dashboard visibility is unchanged by assignments; add a regression test

## 7. Integration check

- [ ] 7.1 Run backend and frontend test suites and a manual pass as admin, manager and employee covering delete, assignment and email entry; verify all pass and no `@taskflow.local` references remain
