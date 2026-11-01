# Tasks

## 1. Backend: assignee candidates API

- [x] 1.1 Add `AssigneeCandidateDto(UserId, FullName, EmployeeCode, ActiveTasks, OverdueTasks, OnTimeRate)` under `Dtos/Monitoring/TaskManagement` and verify `dotnet build` succeeds
- [x] 1.2 Add a repository query returning a project's assignable candidates (active, on the project's allocations, role lacks `tasks.view.all`) and a grouped per-user count of active, overdue, completed and completed-on-time (`CompletedAt <= Deadline`) non-deleted tasks. Share the active/overdue predicates with `WorkloadRepository` so they are defined once. Verify with unit tests on the in-memory/SQLite test context covering inactive users, non-members, Cancelled/deleted tasks and on-time counting
- [x] 1.3 Add a service that returns null for a missing or deleted project, `[]` for an empty team, and otherwise candidates ranked by active ASC, overdue ASC, on-time rate DESC (nulls last), full name ASC, id ASC. Verify unit tests cover each tie-break level, the no-history-last rule, and that allocation percentage has no effect
- [x] 1.4 Add `GET /api/v1/tasks/assignee-candidates?projectId=` to `TasksController` with `[RequirePermission(PermissionKeys.TasksCreate)]`, returning 404 when the service returns null, and register the new types in DI. Verify integration tests in `TasksEndpointsTests.cs` cover 200 with ranked order, 200 `[]` for an empty team, 403 without `tasks.create`, and 404 for an unknown project

## 2. Frontend: data access

- [x] 2.1 Add an `AssigneeCandidate` interface to `task.model.ts` and `getAssigneeCandidates(projectId)` to `task.service.ts`. Verify `task.service.spec.ts` asserts the URL and query parameter

## 3. Frontend: Create Task assignee behaviour

- [x] 3.1 Remove the WIP allocation logic (`suggestAssignee`, `AssigneeSuggestion`, `MAX_TOTAL_PERCENT` import, allocation-based specs) from `task-create.component.{ts,spec.ts}`. Verify the project builds with `ng build`
- [x] 3.2 Wire `projectId.valueChanges` → `switchMap` → `getAssigneeCandidates` with `candidates` and `candidatesState` signals. On load, select `candidates[0]`. On an empty list or an error, clear the assignee. Verify component specs cover: auto-pick of the first candidate, refetch on project change, a stale response discarded when the project changes mid-request, empty team, and request error
- [x] 3.3 Render the dropdown from `candidates` only, with option text `Name (CODE) - N active - M overdue` (overdue omitted when 0) and a "Select a project first" placeholder when no project is chosen. Verify specs assert non-members are absent and the workload text is shown
- [x] 3.4 Show the notices from design D6 (auto-selected hint that disappears on manual change, "No employees are mapped to this project.", "This project's team could not be loaded."). Verify specs cover a manual override that keeps the user's choice and hides the hint
- [x] 3.5 Adapt the copy flow (D7): keep the copied assignee if they are a candidate, otherwise auto-pick and extend the copy notice. Verify specs for both cases
- [x] 3.6 Run `npm test` in `TaskFlow Proj/frontend` and verify the structure check and all specs pass

## 4. Documentation

- [x] 4.1 Update `TF - Documents/TFWorkflow/App WF in MD/04-Monitoring/04.1-Task-Management.md` (Create Task: team-only dropdown, workload display, auto-pick and tie-break rules, override, messages) and verify the section reads correctly against the spec scenarios
- [x] 4.2 Update `TF - Documents/TFTechnicalWorkflow/Tech WF in MD/04-Monitoring/04.1-Task-Management.md` with the new endpoint, permission, DTO and ranking rule, and verify the endpoint path and permission match `TasksController`

## 5. Integration check

- [x] 5.1 Run `dotnet test` for `TaskFlow Proj/backend` and verify all unit and integration tests pass
- [ ] 5.2 Run the app, open Create Task as a Manager, switch between two projects with different teams, and verify the dropdown, workload text, auto-pick, manual override and empty-team message behave as specified
