# Proposal

## Why

The Admin dashboard (shipped in `dashboards-impl`) shows seven flat counters. Requirement 19 (FR-19.1, 19.4-19.8) asks for organisation-wide analytics: department, task, employee, manager and project breakdowns, trends, date and user filters, charts and drill-down. Admins cannot currently see where work is piling up or who is overloaded.

This change is also the **foundation** for the Manager and Employee analytics changes: it introduces the shared filter model, the single scoped task query, the metric definitions and the chart components they reuse.

## What Changes

- New analytics endpoint `GET /api/v1/dashboard/admin/analytics` with filters (date range + date basis, department, project, employee, manager), guarded by `dashboard.admin`
- One shared scoped-task query and one set of metric definitions (status buckets, completion %, overdue) used by every card, chart and drill-down so totals always reconcile
- Admin breakdowns: department-wise, task-wise (status, priority, deadline buckets, completion trend), employee-wise, manager-wise, project-wise, plus the summary cards
- Frontend: add Chart.js (via `ng2-charts`), shared chart components (stacked bar, donut, line, progress bar, KPI card), a shared filter bar and loading/empty/error states
- Drill-down: clicking a chart segment opens the task list pre-filtered to the same records
- HTML mockup of the Admin dashboard (`mockup.html` in this change folder)
- Existing `/dashboard/admin`, `/manager`, `/employee` summary endpoints are left untouched (not breaking)

## Capabilities

### New Capabilities

- `admin-dashboard-analytics`: Organisation-wide analytics, filters, charts and drill-down for the Administrator dashboard

### Modified Capabilities

_None_ (the `dashboards` capability from `dashboards-impl` is not yet archived into `openspec/specs/`; the old counters stay as they are)

## Impact

- Backend: new `Dashboard/Analytics` DTOs and service in Application/Infrastructure, new controller action; reads `Tasks`, `TaskRecipients`, `Users`, `Departments`, `DepartmentManagers`, `ManagerProjects`, `Projects`. No schema change expected, no migration
- Frontend: new dependency `chart.js` + `ng2-charts`; new shared components under `shared/charts`; Admin dashboard page extended; tasks list must accept filter query params for drill-down
- Depends on nothing new; **`manager-dashboard-analytics` and `employee-dashboard-analytics` depend on this change**
- Tests: backend service tests (metric consistency, filters), API tests (403/401), frontend component and service specs
