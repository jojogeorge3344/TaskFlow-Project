# Tasks

## 1. Manager scope (prerequisite: `admin-dashboard-analytics` groups 1 and 3 are done)

- [ ] 1.1 Implement `ManagerScope` resolving the team set (reports union managed-department members, distinct, active) and the scoped task query; verify unit tests for each relation, overlap and the empty team
- [ ] 1.2 Rebase `GetManagerStatsAsync` on the scope; verify a regression test where Manager A no longer counts Manager B's tasks
- [ ] 1.3 Add scope validation for employee and project filters (403 out of scope, 404 unknown) and ignore any department parameter; verify tests for each case

## 2. Manager analytics backend

- [ ] 2.1 Implement task analytics (summary, status, priority) for the team, with no department breakdown; verify totals reconcile in tests
- [ ] 2.2 Implement employee-wise analytics with High workload and Overdue flags in one thresholds class; verify tests around the 1.5x and minimum-5 boundaries
- [ ] 2.3 Implement project-wise analytics (team tasks only, assigned projects with zero tasks) and the team summary attention list; verify tests including projects shared with other teams
- [ ] 2.4 Add `GET /api/v1/dashboard/manager/analytics` and `.../filters` with `dashboard.manager`; verify API tests for 200, 401, 403 (no permission, other team's employee, foreign project)
- [ ] 2.5 Extend the tasks list scope test so tampered drill-down params cannot return out-of-scope tasks; verify the test

## 3. Manager dashboard UI

- [ ] 3.1 Build the Manager analytics page to match `mockup.html` using the shared charts and filter bar; verify component specs per section
- [ ] 3.2 Show flag badges with text, the attention list and links to records; verify specs for flag rendering and empty team
- [ ] 3.3 Wire scoped employee and project filter options, loading, empty and error-with-retry states; verify specs that options never include out-of-scope entries
- [ ] 3.4 Check responsive layout at phone, tablet and desktop widths and verify no horizontal page scroll

## 4. Integration

- [ ] 4.1 End-to-end check with two managers with overlapping departments: each sees only their team, every filter and drill-down count matches the chart values
- [ ] 4.2 Confirm an Employee and an unscoped call receive 403 on the manager analytics endpoint
