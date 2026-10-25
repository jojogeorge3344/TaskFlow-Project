# Design

## Context

`GetEmployeeStatsAsync(userId)` already counts tasks where `AssignedToUserId == userId` or a `TaskRecipient` row exists. `User.DepartmentId` identifies the department, and projects come from task `ProjectId` and `User.ProjectId`. The controller derives the user id from the session claim. `admin-dashboard-analytics` provides the metric buckets, scope interface, chart components and drill-down mapping. See proposal.md for motivation.

## Goals / Non-Goals

**Goals:**
- Personal analytics where the subject cannot be changed by the client
- Aggregate-only department context
- Same metric definitions as Admin and Manager

**Non-Goals:**
- Letting an employee view another employee's analytics ("unless explicitly authorised" is out of scope; no authorisation mechanism exists, so it is not built)
- Entity filters, export, forecasting, customisation

## Decisions

**D1. Identity from the session.** The endpoint takes only `period` (`7d`, `30d`, `month`); user id is read from the `NameIdentifier` claim as the existing endpoint does. Unknown or extra query keys are ignored, never bound to the scope, so an `employeeId` param has no effect. Alternative, accept `employeeId` and check ownership, rejected as it adds a path to get wrong.

**D2. "My tasks" = assigned or recipient**, matching the existing employee counters so numbers do not change unexpectedly between the old and new endpoints. Buckets use the shared exclusive definitions.

**D3. Department overview is aggregate-only.** One grouped query over tasks assigned to members of the user's department, returning counts only; the DTO has no per-user field, so a leak cannot happen by serialisation. Department tasks counted by assignee department, as on the Admin dashboard.

**D4. Project overview scoped to the user.** Projects = projects of the user's own tasks union `User.ProjectId`. Progress = the user's completed tasks over the user's tasks in that project (not the whole project), so no colleague data is implied. Latest deadline = the latest deadline among the user's open tasks in it.

**D5. Period.** Presets Last 7 days, Last 30 days (default), This month, in the application timezone via `IAppClock`; the trend plots created and completed per day with zero-filled days. Upcoming deadlines are always the next 7 days regardless of period.

**D6. Endpoint.** `GET /api/v1/dashboard/employee/analytics?period=` with `[Authorize]`, returning `department`, `summary`, `tasksByStatus`, `tasksByPriority`, `upcomingDeadlines[]`, `projects[]`, `trend[]`, `completedInPeriod`.

**D7. Drill-down.** Uses the shared mapping with an assignee fixed to the caller; the task list scopes by the caller regardless of query params.

## Risks / Trade-offs

- [Department aggregates could reveal a colleague's workload in a tiny department] → counts only; if a department has fewer than 3 members the overview shows a note and hides the numbers beyond the total (decide threshold in review)
- [Old and new employee numbers differ in definition of Pending] → explicit labels; the old endpoint is kept
- [Someone edits the URL to see other users' tasks] → server-side scope tests, not UI hiding
- [Employee with no department or projects] → empty states tested

## Open Questions

- Minimum department size for showing aggregates (default 3); safe to confirm during review
