# Tasks

## 1. Employee analytics backend (prerequisite: `admin-dashboard-analytics` groups 1 and 3 are done)

- [ ] 1.1 Implement the employee scope (own tasks = assignee or recipient) on `IDashboardScope`; verify unit tests for assigned, shared and unrelated tasks
- [ ] 1.2 Implement summary, status and priority buckets, upcoming deadlines (next 7 days) and completed-in-period; verify tests including no tasks
- [ ] 1.3 Implement the aggregate-only department overview (DTO without per-user fields) with the small-department rule; verify tests for no department, small department and a privacy test that the response carries no colleague data
- [ ] 1.4 Implement the project overview (user's own tasks per project, `User.ProjectId` membership, latest open deadline); verify tests where colleagues also have tasks in the same project
- [ ] 1.5 Implement the created/completed trend with zero-filled days for 7d, 30d and month; verify tests across a month boundary in the application timezone
- [ ] 1.6 Add `GET /api/v1/dashboard/employee/analytics?period=` with `[Authorize]`, subject from the session; verify API tests for 200, 401, an ignored `employeeId` param and ignored entity filters

## 2. Employee dashboard UI

- [ ] 2.1 Build the Employee analytics page to match `mockup.html` (department overview, task donuts, upcoming deadlines, summary, my projects, performance trend, period selector); verify component specs per section
- [ ] 2.2 Show Overdue labels in text, deadline rows linking to tasks and the empty states (no tasks, no department, no projects); verify specs
- [ ] 2.3 Wire drill-down to the employee's own task list and verify the list count equals the clicked value; verify a test that a tampered URL returns no other user's tasks
- [ ] 2.4 Wire loading and error-with-retry states and check phone, tablet and desktop widths with no horizontal scroll

## 3. Integration

- [ ] 3.1 End-to-end check as an Employee: only the period selector is available, numbers match the task list, and no colleague data appears in network responses
