# Proposal

## Why

The Employee dashboard shows six personal counters. Requirement 19 (FR-19.3, 19.6, 19.8) asks for a personalised view: department overview, task analytics, project overview, completion trends and upcoming deadlines, without exposing other employees' private task data.

## What Changes

- New endpoint `GET /api/v1/dashboard/employee/analytics` for the signed-in user only. The user id always comes from the session, never from a parameter, so no other employee's data can be requested
- Sections: department overview (department name and aggregate-only stats), my task analytics (status, priority, upcoming deadlines), overall task summary (total, completion %, completed vs outstanding), my projects, personal performance insights (completion trend, overdue, completed in period)
- Department stats are aggregate counts only, no names or tasks of colleagues
- Simple period selector for the employee's own trend and completed-work summary (the requirement mandates date filters only for Admin and Manager, so no entity filters are offered)
- Frontend employee page reusing shared charts from `admin-dashboard-analytics`
- HTML mockup of the Employee dashboard (`mockup.html` in this change folder)

## Capabilities

### New Capabilities

- `employee-dashboard-analytics`: Personal task, project, department-summary and trend analytics for Employees

### Modified Capabilities

_None_ (the `dashboards` capability from `dashboards-impl` is not yet archived into `openspec/specs/`)

## Impact

- Backend: employee scope rule in `IDashboardScope`, new endpoint, aggregate-only department summary; no schema change
- Frontend: Employee dashboard page extended; no entity filters
- **Depends on `admin-dashboard-analytics`** (metric definitions, chart components, scope interface); implement that change first
- Tests: privacy tests (no colleague data in responses), session-derived identity, empty states
