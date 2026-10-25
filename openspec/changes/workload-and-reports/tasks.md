# Tasks

## 1. Workload backend

- [x] 1.1 Add workload DTOs (per-employee active, high-priority, overdue and completed counts plus department) and an `IWorkloadService`/`WorkloadService` that computes them for every active user with an optional department filter, using `IAppClock` for the overdue rule. Verify with unit tests: counts are correct, the department filter narrows the result, zero-load employees still appear, and overdue matches the dashboard rule.
- [x] 1.2 Add `WorkloadController` with `GET /api/v1/workload` behind the `ManagerOrAdmin` policy. Verify with integration tests: a Manager and an Administrator can read it, an Employee receives 403, and a `departmentId` filter narrows the returned employees.

## 2. Reports backend

- [x] 2.1 Add report DTOs (filter plus totals and a completion summary) and a validator, and an `IReportService`/`ReportService` computing total, pending, completed and overdue for the filter — deadline date range, employee, department, status and priority. Verify with unit tests for filter combinations, an empty scope returning zeros, and overdue consistency with the dashboard rule.
- [x] 2.2 Add `ReportController` with `GET /api/v1/reports` behind the `ManagerOrAdmin` policy. Verify with integration tests: a Manager can read it, an Employee receives 403, and combined filters return only matching rows.
- [x] 2.3 Add the CSV export endpoint `GET /api/v1/reports/export` returning `text/csv` as an attachment, with formula-injection protection on leading `=`, `+`, `-` and `@`. Verify with a test asserting the header row, the escaping of a leading `=`, and that exported rows match the same filter as the totals.

## 3. Frontend services and models

- [x] 3.1 Add models (`EmployeeWorkload`, `WorkloadSummary`, `ReportFilter`, `ReportSummary`) and `workload.service.ts` and `report.service.ts`. Verify with `npm run build` and service specs asserting the expected endpoints are called.

## 4. Workload screen

- [x] 4.1 Build `workload.component` with a department filter, the per-employee table, a CSS bar visualisation, the high-load highlight (at or above twice the average active count) and links into the task list filtered by that employee; register route `/workload` with `roleGuard(['Administrator','Manager'])` and add the nav entries for both roles. Verify with a spec covering department filtering, the highlight rule and the task-list link.

## 5. Reports screen

- [x] 5.1 Build `report.component` with the filter toolbar, headline totals, a completion summary bar and a CSV download; register route `/reports` with `roleGuard(['Administrator','Manager'])` and add the nav entries for both roles. Verify with a spec covering applying filters, rendering the totals and triggering the CSV download.

## 6. Integration verification

- [x] 6.1 Run the backend `dotnet test` suite and the frontend `npm test` and `npm run build`; confirm no new failures or warnings versus the pre-change baseline, and that workload and report figures agree with the dashboards for the same data.

## Workflow follow-up

- After code review, archive this change so its deltas sync into the main specs for `employee-workload` and `reports`.
