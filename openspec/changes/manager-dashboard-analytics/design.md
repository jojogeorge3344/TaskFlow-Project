# Design

## Context

`DashboardService.GetManagerStatsAsync` uses `ActiveTasks()` with no scope, so any manager sees organisation-wide counts. Relations available: `User.ManagerId` (direct reports), `DepartmentManager` (managed departments, edited in Department Management), `ManagerProject` (documented as recorded-only, not narrowing). `admin-dashboard-analytics` supplies `IDashboardScope`, the metric buckets, filter model, chart components and drill-down mapping. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Scope enforced in the query, not in the UI
- Reuse the Admin metric definitions and components unchanged
- Reject out-of-scope filters explicitly

**Non-Goals:**
- Changing how managers are assigned to departments, reports or projects
- Letting `ManagerProject` narrow scope (it remains informational)
- Optional features (export, forecasting, customisation)

## Decisions

**D1. Scope = reports union managed-department members (confirmed).** `ManagerScope` resolves a distinct set of user ids once per request: `Users where ManagerId == me` union `Users where DepartmentId in my DepartmentManager rows`, active and non-deleted. It excludes the manager themself unless they are in the set via a relation. Tasks in scope = assigned to a team member, or shared with a team member through `TaskRecipient`. Alternative, only one of the two relations, rejected: departments and direct reports are edited independently.

**D2. Plug into `IDashboardScope`.** The manager implementation filters the base task query by the team set before filters apply, so no aggregate can see more than the team. The existing `GetManagerStatsAsync` is rebased on the same scope (security fix) and keeps its DTO shape.

**D3. Out-of-scope filter ids give 403, unknown ids give 404.** A manager must not be able to learn whether an out-of-scope id exists beyond what the UI already shows; 403 for any employee or project id not in the manager's team or projects, 404 only when the id does not exist at all and cannot be probed to reveal membership. Chosen to keep the check a single set lookup.

**D4. Project scope.** Projects listed = `ManagerProject` rows plus projects that team tasks belong to. Counts only include team tasks. A project is a valid filter value if it is in that list.

**D5. Flags are computed server-side.** `HighWorkload` = open tasks at least 1.5 times the team mean and at least 5; `HasOverdue` = overdue count above 0. Thresholds are constants in one class so they can be tuned. The attention list returns up to five most overdue tasks and the projects with overdue tasks.

**D6. Endpoint.** `GET /api/v1/dashboard/manager/analytics?from&to&basis&projectId&employeeId` plus `.../manager/analytics/filters` returning only in-scope employees and projects. A `departmentId` parameter is not part of the contract and is ignored. Response reuses the shared bucket DTOs.

**D8. No department-wise view.** Managed departments are used only to resolve the team (D1). The manager dashboard has no department chart, table or filter; the department grouping in the shared aggregates is simply not requested for the manager.

**D7. Drill-down.** Uses the shared mapping. The task list API limits a manager (holds `dashboard.manager` but not `dashboard.admin`) to their team whenever any drill-down parameter is present (bucket, deadlineBucket, department, project, employee, manager, from or to), so tampered drill-down params cannot widen results (tested). Scope of this guarantee: it covers the dashboard and its drill-down only. The plain Tasks screen, board and task details are unchanged, because managers hold `tasks.view.all` and the existing `AssignmentsDoNotChangeWhatAManagerCanSee` contract says assignments never change what a manager sees there. Scoping those screens for managers would be a separate change that replaces that contract.

## Risks / Trade-offs

- [Scope leak through a missed query] → every aggregate derives from the one scoped query; tests with two managers and overlapping data assert no cross-visibility
- [A user is in two managers' teams] → both managers see them; accepted and documented
- [Behaviour change to the existing manager counters] → intended; called out in release notes and a regression test with a known team
- [Manager who is also assigned tasks] → their own tasks are in scope only if they are in the team set; documented

## Open Questions

- Whether the High workload thresholds (1.5x mean, minimum 5) suit real teams; safe to tune after first use
