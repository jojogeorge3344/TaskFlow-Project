# Design

## Context

`DashboardService` (Infrastructure) returns flat counts via one `ActiveTasks()` query. `Project` has only name/description/created date, `Task` has no department (derive it from `AssignedTo.DepartmentId`), `Task.CompletedAt`/`CreatedAt`/`Deadline`/`StartDate` exist, and `TaskRecipient` links extra participants. Manager relations exist in `User.ManagerId`, `DepartmentManager` and `ManagerProject`. The frontend is Angular 19 with no chart library. See proposal.md for motivation.

This change owns the pieces that `manager-dashboard-analytics` and `employee-dashboard-analytics` reuse.

## Goals / Non-Goals

**Goals:**
- One metric definition and one scoped task query so cards, charts and drill-down always agree
- A request/response contract (`AnalyticsFilter`, bucket counts) that the manager and employee changes reuse
- Reusable chart components and filter bar

**Non-Goals:**
- Project deadline/status columns (no schema change; projected end is derived)
- Export, forecasting, dashboard customisation, automated insights, ranking (optional features in Req 19 section 4)
- Changing the existing summary endpoints

## Decisions

**D1. Chart.js via `ng2-charts`, not hand-rolled SVG.** Gives stacked bar, donut, line, tooltips, legends and click events needed by FR-19.4 and drill-down with little code. Cost: one new dependency (`dashboards-impl` added none). Alternative, custom SVG components, rejected as five chart types plus tooltips and click handling is a lot of code to build and test.

**D2. Exclusive status buckets.** Completed / Overdue / In Progress / Pending, mutually exclusive, cancelled excluded. Overdue takes precedence over open states. Rationale: stacked bars and donuts must sum to the total. Consequence: the new "Pending" is narrower than `AdminDashboardDto.PendingTasks` (which is all open tasks); the new endpoint is separate so the old one is unaffected, and the UI labels are explicit.

**D3. One scoped query, built by a single `IDashboardScope`.** `IDashboardScope.Tasks(filter, caller)` returns `IQueryable<Task>` after applying role scope (admin: all; manager and employee changes plug in their own rule) and then the filter (dates, department, project, employee, manager). All aggregates are projections of that query. Alternative, per-chart queries with per-chart filters, rejected because that is exactly how totals drift.

**D4. Date basis.** Range applies to `CreatedAt` (default), `Deadline` or `CompletedAt`, all evaluated as local-day boundaries converted to UTC with the existing `ReminderCalculator`/`IAppClock` helpers. Status is always the task's current status. The trend chart always plots created and completed per day regardless of basis, and says so. Default range Last 30 days.

**D5. Attribution rules.** Department and manager charts attribute a task to its assignee (one task, one group, totals reconcile). Employee chart and employee filter count a task for the assignee and every recipient, matching the existing employee dashboard; the chart footnote explains totals may exceed the task count. Manager team = direct reports plus members of managed departments (union, distinct). A user in two managers' teams appears under both.

**D6. Project progress is derived.** Progress = completed / total tasks; projected end = max task deadline. Tasks without a project group as "No project"; users without a department group as "No department".

**D7. API shape.** `GET /api/v1/dashboard/admin/analytics?from&to&basis&departmentId&projectId&employeeId&managerId` returning one DTO: `summary`, `tasksByStatus`, `tasksByPriority`, `deadlineBuckets`, `trend[]`, `byDepartment[]`, `byEmployee[]` (top 10 + total count), `byManager[]`, `byProject[]`. One round trip keeps sections consistent; sections render their own loading/empty states from it. `[RequirePermission(PermissionKeys.DashboardAdmin)]`. Invalid ids give 404, invalid ranges 400. Filter options (lists of departments, projects, employees, managers) come from `GET .../admin/analytics/filters`.

**D8. Drill-down by query params.** Chart clicks navigate to the task list with query params (`status`, `priority`, `departmentId`, `projectId`, `assigneeId`, `from`, `to`, `basis`). The task list API already applies the caller's scope; it needs the new filters added so drilled-down counts equal chart values (verified by a test).

**D9. Frontend structure.** `shared/charts` (kpi-card, stacked-bar, donut, line, progress-list), `shared/dashboard-filter-bar`, a `DashboardAnalyticsService`, and a signal store holding the filter. Requests use `switchMap` so a stale response cannot overwrite a newer one. Colours come from the existing `--tf-*` tokens; status is shown with text labels too.

## Risks / Trade-offs

- [Totals drift between sections] → single scoped query, a service test asserting bucket sum equals total for varied filters
- [Slow aggregates on large data] → server-side `GroupBy`, no per-row loops, indexes on `Tasks(AssignedToUserId, Status, Deadline)` verified in tasks; a measured test with seeded data
- [Employee chart totals exceed task total] → footnote and doc, test for recipient counting
- [Department changes retroactively move history] → accepted and documented; tasks carry no department snapshot
- [Wider "Overdue" precedence confuses vs old cards] → explicit labels and a short legend tooltip
- [Drill-down mismatch] → shared filter-to-query mapping with an integration test

## Open Questions

- Which week start to use for weekly trend buckets (default Monday); safe to decide at build time
