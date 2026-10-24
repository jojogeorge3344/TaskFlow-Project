# Design

## Context

Backend (.NET, EF Core): a project team is `UserProjectAllocation` rows (user, project, position, percent), replaced as a whole by `UserAllocationService.ReplaceProjectMembersAsync` inside one transaction. "Manager oversees project" is `ManagerProject`, edited through `UserService.UpdateManagerAssignmentsAsync` (route `PUT users/{id}/assignments`). A user's managers and reports are derived from departments in `UserService`, and `ManagerDashboardScope.TeamUserIds` scopes a manager to their department members and uses `ManagerProject` plus team tasks to find projects. Frontend (Angular): `project-team-dialog.component.ts` edits team rows; the employee form panel edits a manager's projects. See proposal.md for motivation and scope.

## Goals / Non-Goals

**Goals:**
- One place, the team dialog, decides a project's managers.
- Derived data (manager link, employee managers, reports, scope) follows the flags with no second source.

**Non-Goals:**
- Changing department-based management or its screens.
- New permissions or notifications.
- Backfilling flags for existing projects.

## Decisions

1. **Store the flag on the allocation row.** Add `IsProjectManager` (bool, default false) to `UserProjectAllocation`. A flagged manager is a normal team row, so position, percent and the 100% cap already apply.
   *Alternative:* a separate `ProjectManagers` table. Rejected: it can disagree with the team and needs its own cleanup when someone leaves it.

2. **Keep `ManagerProject` as a derived table.** `ReplaceProjectMembersAsync` rewrites the links for that project from the flags in the same transaction. Existing readers (`UserDto.ManagerProjects`, `ManagerDashboardScope.ProjectIds`, analytics) keep working unchanged.
   *Alternative:* drop `ManagerProject` and read flags everywhere. Rejected: wider change across analytics for no user-visible gain.

3. **Validate on the server, mirror in the UI.** `ProjectMemberInput` gains `IsManager`. For a non-empty team the service requires at least one flag and that every flagged user has `SystemRoles.ManagerId`; otherwise it throws `InvalidOperationException`, which the existing controller path maps to a 400/409. The dialog disables the flag for non-Managers, shows "Mark at least one manager", and blocks Save, using the same rules.

4. **Remove the direct assignment route.** Delete `PUT users/{id}/assignments`, `UpdateManagerAssignmentsAsync` and `ManagerAssignmentRequest`, with their tests. The Edit employee panel shows `managerProjects` read-only and stops calling the endpoint. **BREAKING**, but the only client is this frontend.
   *Alternative:* keep the route and let it desync. Rejected: two writers for one fact.

5. **Derive managers and reports from both sources.** In `UserService`, a user's managers are the distinct union of department managers and flagged managers of the user's projects; a manager's reports are the union of department members and members of the projects they manage. Exclude the person themself, as today.

6. **Widen manager scope.** `ManagerDashboardScope.TeamUserIds` becomes the active users who are department members of a managed department or members of a project the manager is linked to. It stays an `IQueryable<int>` so existing callers do not change.
   *Risk:* more people appear on manager dashboards. This follows the decision to link flags to existing scope, and has its own task group so it can be dropped without touching the rest.

7. **Project list carries manager names.** `ProjectWithCountDto` gains a `Managers` list of names loaded in the same query; `ProjectMemberDto` gains `IsManager`. No extra endpoints.

8. **Legacy data is not migrated.** The migration only adds the column. Old `ManagerProject` rows stay until that project's team is saved, when they are replaced from the flags.

## Risks / Trade-offs

- [Old manager links vanish on the next team save] → Document it in the dialog's error and the release note; the administrator flags the right people at that moment.
- [Flagged manager consumes percentage] → The dialog already shows the remaining cap per person; no special case.
- [Scope widening exposes more data to managers] → Only active project members, only for projects the manager is flagged on; covered by isolation tests.
- [Removing the route breaks any script using it] → Only the frontend used it; search the repo before deleting.
- [A project with a team but no manager is possible for legacy rows] → Allowed until the next save by decision.

## Migration Plan

1. Add the column with a default of false (non-breaking) and deploy.
2. Deploy the API and frontend together, since the old frontend would call the removed route.
3. Rollback: revert the deploy and drop the column; derived links remain valid.

## Open Questions

- None that change the specs or tasks.
