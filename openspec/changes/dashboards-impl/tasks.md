## 1. Preconditions

- [x] 1.1 Verify build/tests still green after departments/employees implementation.

## 2. Backend - Dashboard DTOs & Service

- [x] 2.1 Add DTOs in `TaskFlow.Application/Dashboard/` (AdminDashboardDto, ManagerDashboardDto, EmployeeDashboardDto with relevant fields).
- [x] 2.2 Define `IDashboardService` with GetAdminStatsAsync, GetManagerStatsAsync, GetEmployeeStatsAsync.
- [x] 2.3 Implement `DashboardService` in `TaskFlow.Infrastructure/Services/` using EF Core queries for counts/summaries.
- [x] 2.4 Register `IDashboardService` in DI.
- [x] 2.5 Replace the hardcoded zero statistics in `DashboardService` with real EF Core queries where the `Task`/`TaskRecipient` model supports them (total/pending/completed/overdue tasks, tasks due today, upcoming deadlines, in-progress, assigned-per-employee). The `Task` entity, `TaskRecipient` entity and their `DbSet`s already exist; use `Status`, `Deadline`, `CompletedAt` and assignment fields. Do not fabricate values: if a specific metric cannot be derived yet, surface the gap explicitly (document it and/or omit the field rather than returning a misleading zero) and note it in the final summary.
- [x] 2.6 Add/renew backend unit tests covering `DashboardService` aggregate queries (counts, due-today boundary, overdue filter, employee scoping).

## 3. API Layer

- [x] 3.1 Add `DashboardController` with GET `/api/v1/dashboard/admin` (Admin), GET `/api/v1/dashboard/manager` (Manager/Admin), GET `/api/v1/dashboard/employee` (Employee/Manager/Admin as appropriate per RBAC) returning stats.
- [x] 3.2 Verify each dashboard endpoint returns populated stats after 2.5 (integration test or documented manual call), and denies access to the wrong role.

## 4. Frontend - Models & Service

- [x] 4.1 Add dashboard models in `frontend/src/app/core/models/dashboard.model.ts` (`AdminDashboardStats`, `ManagerDashboardStats`, `EmployeeDashboardStats`) whose field names mirror the backend DTOs (`TaskFlow.Application/Dashboard/DashboardDto.cs`); export them from `frontend/src/app/core/models/index.ts`.
- [x] 4.2 Add `DashboardService` in `frontend/src/app/core/services/dashboard.service.ts` with `getAdminStats()`, `getManagerStats()`, `getEmployeeStats()` calling `/api/v1/dashboard/admin|manager|employee` using the existing `/api/v1` base URL convention; rely on the existing `authInterceptor` for the bearer token (no manual auth wiring). Do not hardcode statistics when backend data is available.

## 5. Frontend - Shared Layout

- [x] 5.1 Create one shared `DashboardLayoutComponent` (`frontend/src/app/features/dashboard/dashboard-layout.component.ts`) that renders the role dashboard content plus a responsive sidebar; the current user's role drives which navigation links appear.
- [x] 5.2 Define role-specific navigation menus from `AuthService.currentUser().role`: Administrator (Dashboard, Employee Management, Department Management, Task Management), Manager (Dashboard, Tasks, ...), Employee (Dashboard, My Tasks, ...). Reuse existing routes where they exist; only link to routes that exist (do not invent dead links).
- [x] 5.3 Make the layout responsive (desktop sidebar, collapsible/off-canvas on mobile) using the project's existing styling conventions/design tokens.
- [x] 5.4 Include the signed-in user's name and a working Sign out action in the layout (preserve existing logout behaviour).

## 6. Frontend - Role Dashboard Components

- [x] 6.1 Create `AdminDashboardComponent`: summary cards for total employees, departments, tasks, completed tasks, and pending tasks; welcome message for the logged-in administrator; hosted by `DashboardLayoutComponent`.
- [x] 6.2 Create `ManagerDashboardComponent`: team/task statistics with sections for assigned, completed, pending, and overdue tasks plus task progress; welcome message; hosted by `DashboardLayoutComponent`.
- [x] 6.3 Create `EmployeeDashboardComponent`: personal pending, completed, and overdue statistics plus a task list and progress summary; welcome message; hosted by `DashboardLayoutComponent`.
- [x] 6.4 Handle loading states, API errors, and empty results in all three components (visible loader, error message, and an explicit empty state) — never render fabricated numbers when the request fails or returns nothing.

## 7. Routing

- [x] 7.1 Update `app.routes.ts` so `/admin` loads `AdminDashboardComponent`, `/manager` loads `ManagerDashboardComponent`, and `/me` loads `EmployeeDashboardComponent`; keep `authGuard`, `roleGuard`, and the `data.roles` values exactly as they are.
- [x] 7.2 Keep login redirect (`dashboardRouteFor(role)`) and logout working; retire the old generic `DashboardComponent` if it is no longer referenced (no duplicate/orphan components).

## 8. Tests

- [x] 8.1 Unit tests for frontend `DashboardService` (correct endpoints per role, typed responses, error propagation).
- [x] 8.2 Component tests for `AdminDashboardComponent`, `ManagerDashboardComponent`, `EmployeeDashboardComponent` (renders stats, loading, error, empty states).
- [x] 8.3 Component tests for `DashboardLayoutComponent` (role-scoped menus, mobile toggle, sign-out).
- [x] 8.4 Update `app.routes.spec.ts` to assert the routes load the role-specific components while preserving the existing guard/role assertions.

## 9. Verification

- [x] 9.1 Frontend builds (`npm run build`) and tests pass (`npm run test`).
- [x] 9.2 Backend builds and tests pass (`dotnet test`).
- [x] 9.3 Manual smoke: each role opens the correct dashboard, sees role-relevant (non-fabricated) stats, cannot access another role's dashboard (guard redirect), and login/logout still work.
- [x] 9.4 Summarize files created/modified, tests performed, and any remaining backend statistics gaps identified in 2.5.
