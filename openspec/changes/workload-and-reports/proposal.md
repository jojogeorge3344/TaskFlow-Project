# Proposal

## Why

The dashboards show aggregate task counts, but neither of the two remaining "manager insight" screens exists. Administrators and Managers cannot see how work is distributed across employees (screen 16), nor produce a filtered, exportable report of task allocation and completion (screen 17). Both require aggregating tasks against users and departments across the authorized scope, which the backend does not expose today.

## What Changes

- **Employee workload API** — an Administrator or Manager can read per-employee task counts (active, high-priority, overdue, completed), optionally filtered by department. Employees do not receive this view.
- **Reports API** — an Administrator or Manager can read task totals and a completion summary for a filtered scope (date range, employee, department, status, priority), and export the underlying rows as CSV.
- **Workload screen (`/workload`)** — a per-employee table with a simple bar visualisation, a department filter, a warning highlight for unusually high active-task counts, and links into the task list filtered by that employee.
- **Reports screen (`/reports`)** — filter controls, headline totals, a completion summary visualisation and a CSV download, consistent with the status and overdue rules used elsewhere.
- **Access control** — both screens and their endpoints are limited to Administrators and Managers; queries are restricted to that scope so no user sees data outside their authorized view.
- **No new dependency** — visualisations use the existing CSS primitives and layout conventions already used by the dashboards rather than introducing a charting library.

## Capabilities

### New Capabilities

- `employee-workload`: An Administrator or Manager can read a per-employee distribution of task load across the organization, filtered by department, to support task allocation decisions.
- `reports`: An Administrator or Manager can produce a filtered summary of task allocation, status and completion within their authorized scope and export the underlying task rows as CSV.

### Modified Capabilities

<!-- None: no existing main-spec requirements change. -->

## Impact

- **Backend / API**: new `GET /api/v1/workload` and `GET /api/v1/reports` (plus a CSV variant) endpoints behind a `ManagerOrAdmin` policy. New read-only services and DTOs aggregating the existing `Task`, `User` and `Department` tables. Overdue and due-today calculations reuse the timezone-aware `IAppClock`, so figures stay consistent with dashboards and reminders.
- **Database**: no schema change — both capabilities aggregate data that already exists.
- **Frontend**: new `features/workload/` and `features/reports/` screens, `workload.service.ts` and `report.service.ts`, new models, routes as children of the shell, and `ROLE_NAVIGATION` entries for Administrator and Manager. Reuses the existing `tf-` primitives and task-filter conventions.
- **Tests / Docs**: unit and integration coverage for the aggregation and export endpoints, and frontend specs for both screens. Requirement files `16-employee-workload-screen.md` and `17-reports-screen.md` are the source of truth for scope; the charting and stack notes in those files are interpreted against the implemented .NET + Angular + CSS stack.
