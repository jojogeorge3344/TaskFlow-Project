# Tasks

## 1. Data model

- [x] 1.1 Add `IsProjectManager` (bool, default false) to `UserProjectAllocation`, map it in `AppDbContext`, and add an EF migration that only adds the column; verify with `dotnet ef migrations list` and that the model snapshot matches
- [x] 1.2 Add `IsManager` to `ProjectMemberInput` and `ProjectMemberDto`, and `Managers` (names) to `ProjectWithCountDto`; verify the solution builds with `dotnet build`

## 2. Team save rules

- [x] 2.1 In `UserAllocationService.ReplaceProjectMembersAsync`, require at least one flagged member for a non-empty team and require every flagged user to hold the Manager role, throwing `InvalidOperationException` with clear messages; verify with unit tests for no flag, non-manager flagged, empty team, and several managers
- [x] 2.2 Persist the flag on each allocation row and rewrite the project's `ManagerProject` links from the flags in the same transaction (add for flagged, remove for the rest and for removed members); verify with unit tests that swapping the manager moves the link and that removing a flagged person removes it
- [x] 2.3 Make `ProjectService.GetMembersAsync` and `GetAllAsync` return the flag and manager names; verify with unit tests and the integration test for `GET /api/projects` and the members endpoint
- [x] 2.4 Verify `PUT /api/projects/{id}/members` returns the validation error for a team with no manager and changes nothing, with an integration test covering success, no manager, and non-manager flagged

## 3. Derived managers and scope

- [x] 3.1 In `UserService`, build each user's managers as the distinct union of department managers and flagged managers of their projects, and each manager's reports as department members plus members of their projects; verify with unit tests including the duplicate case and the person themself excluded
- [x] 3.2 Widen `ManagerDashboardScope.TeamUserIds` to include active members of projects the manager is linked to; verify with unit tests and an isolation integration test that a project member appears and an unrelated employee does not
- [x] 3.3 Run the existing analytics and manager scope tests and fix any expectation that assumed department-only teams; verify `dotnet test` passes

## 4. Remove direct assignment

- [x] 4.1 Search the repo for callers of `UpdateManagerAssignmentsAsync`, `ManagerAssignmentRequest` and `PUT users/{id}/assignments`; verify only the employee form panel uses them
- [x] 4.2 Delete the route, service method, request type, validator and their tests (`ManagerAssignmentTests`, `ManagerAssignmentEndpointsTests`); verify `dotnet build` and `dotnet test` pass

## 5. Team dialog

- [x] 5.1 Add `isManager` to the team row model and a Manager toggle per row in `project-team-dialog.component.ts`, disabled with a short hint for people who are not Managers; verify with a spec for enabled and disabled states
- [x] 5.2 Require at least one flagged row when the team has members: show "Mark at least one manager" and disable Save until satisfied, and send `isManager` in the request; verify with specs for no flag, one flag, several flags and an empty team
- [x] 5.3 Pre-fill the flags when opening an existing team, and surface the API's manager error inside the dialog; verify with specs for loading flags and for the server error
- [x] 5.4 Update `project.model.ts`, `project.service.ts` and their specs for `isManager` and `managers`; verify the service specs pass

## 6. Project list

- [x] 6.1 Show each project's manager names on its card and "No manager yet" when none; verify with a project list spec for both cases

## 7. Employee screen

- [x] 7.1 Show "Projects this manager oversees" as a read-only list on the Edit employee panel, remove the picker and the `updateAssignments` call from `employee-form-panel` and `user.service.ts`; verify the form panel and service specs are updated and pass
- [x] 7.2 Show a person's project managers among their managers on the employee details panel; verify with a details panel spec

## 8. Integration checks

- [x] 8.1 Run `dotnet test` and the frontend `ng test` and `ng build`; verify all pass
- [ ] 8.2 With the app running, create a project, assign a team with a manager flag, check the project list, the employee view, and the manager's dashboard, and record any mismatch

## Workflow follow-up

- Archive the change after review and manual sign-off.
