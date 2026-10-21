# Proposal

## Why

The Manager dashboard shows six counters and, worse, counts **every task in the organisation**: `GetManagerStatsAsync` has no scoping. Requirement 19 (FR-19.2, 19.5, 19.6, 19.8, acceptance criteria 2 and 9) requires managers to see analytics only for their own employees and projects (their scope is derived from the departments they manage and their direct reports), with team performance, filters and drill-down.

## What Changes

- Define a manager's authorised scope: their **direct reports** (`User.ManagerId`) plus **members of departments they manage** (`DepartmentManager`), as a distinct union. Projects are shown (from `ManagerProject` and from the team's tasks) but do not narrow scope
- New endpoint `GET /api/v1/dashboard/manager/analytics` with date and entity filters, guarded by `dashboard.manager`, enforcing scope in the query
- Reject (403) any employee or project filter outside the manager's scope
- Manager sections: employee-wise (with high-workload / overdue flags), project-wise, task analytics (status and priority), team performance summary (delayed tasks and projects needing attention)
- Frontend manager page reusing the shared charts and filter bar from `admin-dashboard-analytics`, with scoped filter options
- Close the existing unscoped-counter gap: the current `/dashboard/manager` summary also becomes scoped
- HTML mockup of the Manager dashboard (`mockup.html` in this change folder)

## Capabilities

### New Capabilities

- `manager-dashboard-analytics`: Scoped team, employee and project analytics (no department-wise view) with filters and drill-down for Managers

### Modified Capabilities

_None_ (the `dashboards` capability from `dashboards-impl` is not yet archived into `openspec/specs/`)

## Impact

- Backend: manager scope rule plugged into `IDashboardScope`; new manager analytics endpoint; `GetManagerStatsAsync` updated to use the scope (behaviour change: counts shrink to the team; this is the intended security fix)
- Frontend: Manager dashboard page extended; filter options come from a scoped endpoint
- **Depends on `admin-dashboard-analytics`** (shared scope interface, metric definitions, chart components, filter bar, drill-down mapping); implement that change first
- Tests: scope leak tests (manager A vs manager B), 403 cases, metric consistency, component specs
