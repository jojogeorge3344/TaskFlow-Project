# Proposal

## Why

A project's team is saved today as a list of people with a position and a percentage, and nothing says who manages the project. "Projects this manager oversees" is a separate link that an administrator sets on the employee screen, so the people on a project and its manager are recorded in two unrelated places. The team screen should be where the project's manager is chosen, and everyone on the team should then have that person as their manager for the project.

## What Changes

- Each row in the project team dialog gets a **Manager** flag. At least one person on a team that has members must be flagged, and more than one may be. Saving a team without one is refused, in the UI and in the API.
- Only a person with the Manager role can be flagged.
- The flagged people become the project's managers. Saving a team keeps the "manager oversees project" link in step with the flags, so that link is no longer edited by hand.
- A person's managers now include the flagged managers of every project they are on, in addition to their department managers. A manager's reports include the members of the projects they manage.
- The manager dashboard and team scope include the members of the projects a manager manages, in addition to their department members.
- The project list shows each project's managers.
- The Edit employee screen shows "Projects this manager oversees" as read-only. **BREAKING:** the endpoint that sets a manager's projects directly is removed, so manager-to-project assignment happens only on the project team screen.
- Existing projects are not migrated. They keep working, and a manager must be flagged the next time their team is saved.
- Out of scope: department-based management, task permissions, and notifications.

## Capabilities

### New Capabilities
- `project-team-manager`: marking one or more team members as a project's managers, the rule that a team has at least one, and how that choice drives manager links, employee managers and manager scope.

### Modified Capabilities
<!-- None. The project has no main specs under openspec/specs/. -->

## Impact

- Backend: `UserProjectAllocation` gets an `IsProjectManager` column (new migration). `UserAllocationService.ReplaceProjectMembersAsync`, `ProjectService`, `ProjectMemberDtos` and `ProjectDto` change. `UserService` derives managers and reports from project flags as well. `ManagerDashboardScope.TeamUserIds` widens. `UserService.UpdateManagerAssignmentsAsync` and its controller route are removed.
- Frontend: `project-team-dialog.component.ts`, the project list, project and user models and services, and the employee form panel's manager-projects section.
- Tests: unit and integration tests for team replace, derived managers and scope, plus the frontend specs for the team dialog, project list and employee panel.
- No new dependencies.

## Assumptions

- A team with no members needs no manager; clearing the whole team clears the flags and links.
- A flagged manager has a position and a percentage like any other team row, and that percentage counts toward their 100% cap.
- Skipping the team dialog after creating a project is still allowed. The project has no manager until a team is saved.
- Two earlier decisions are reversed: the manager-projects picker on the Edit employee screen (kept editable in `modern-form-ui-refresh`) becomes read-only.
