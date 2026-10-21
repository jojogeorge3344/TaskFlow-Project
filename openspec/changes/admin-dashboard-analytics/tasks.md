# Tasks

## 1. Metric definitions and scope foundation (shared)

- [ ] 1.1 Add `AnalyticsFilter` (from, to, basis, departmentId, projectId, employeeId, managerId) and bucket/summary DTOs under `Application/Dashboard/Analytics`; verify the project builds
- [ ] 1.2 Implement `IDashboardScope` with the admin scope plus filter application (date basis in application timezone, entity filters, manager team = reports union managed-department members); verify with unit tests for each filter and for the team union
- [ ] 1.3 Implement bucket classification and completion % in one place (exclusive buckets, cancelled/deleted excluded, 0 on empty); verify with a test that the buckets sum to the total across filter combinations

## 2. Admin analytics backend

- [ ] 2.1 Implement summary cards (employees, managers, departments, projects, task buckets, completion %); verify numbers against seeded data in a service test
- [ ] 2.2 Implement task-wise aggregates (status, priority, deadline buckets, created/completed trend with zero-filled days, weekly buckets over 31 days); verify with tests including empty ranges
- [ ] 2.3 Implement department-wise, employee-wise (assignee + recipients, top 10 + total), manager-wise and project-wise aggregates including "No department"/"No project" groups; verify totals reconcile in tests
- [ ] 2.4 Add `GET /api/v1/dashboard/admin/analytics` and `.../filters` with `dashboard.admin`; return 400 for bad ranges and 404 for unknown ids; verify API tests for 200, 401, 403 and error cases
- [ ] 2.5 Review query shape (server-side grouping, no N+1) and add an index on tasks if the query plan needs it; verify with a seeded-data timing check and a migration test if an index is added

## 3. Frontend foundation

- [ ] 3.1 Add `chart.js` and `ng2-charts` and register chart types; verify `ng build` succeeds
- [ ] 3.2 Add shared chart components (kpi-card, stacked-bar, donut, line, progress-list) with legends, tooltips, text labels and click output; verify component specs including empty and loading inputs
- [ ] 3.3 Add `dashboard-filter-bar` (presets, custom range, basis, entity selects, reset) and `DashboardAnalyticsService` with `switchMap` refresh; verify specs for range validation and stale-response handling
- [ ] 3.4 Map chart clicks to task-list query params in one helper and extend the tasks list and its API to accept status, priority, department, project, assignee, range and basis; verify a test that a drilled-down list count equals the clicked value

## 4. Admin dashboard UI

- [ ] 4.1 Build the Admin analytics page to match `mockup.html` (cards, department stacked bar, task donuts and trend, employee, manager and project sections); verify with component specs for each section
- [ ] 4.2 Wire loading, empty and error-with-retry states per section; verify specs that one failing request leaves the filter bar usable
- [ ] 4.3 Check responsive layout at phone, tablet and desktop widths in the browser and verify no horizontal page scroll

## 5. Integration

- [ ] 5.1 End-to-end check as Administrator: apply each preset and a custom range, each entity filter, and a drill-down; verify card, chart and list numbers agree
- [ ] 5.2 Verify a Manager and an Employee receive 403 from the admin analytics endpoint when called directly
