## Why

User login, departments, and employees are implemented. Next are the role-scoped dashboards (Screens 2/3/4): Admin, Manager, Employee. The current frontend dashboard is just a placeholder. Per TDD, we need a `DashboardService` with role-scoped endpoints and dashboard UI components that show statistics relevant to each role.

## What Changes

- Backend `DashboardService` (Application) with methods for admin/manager/employee stats; implement in Infrastructure using EF queries
- Replace the hardcoded zero statistics in `DashboardService` with real EF Core queries against the existing `Task`/`TaskRecipient` model; document any metric that cannot yet be derived rather than fabricating it
- API endpoints: `GET /api/v1/dashboard/admin`, `GET /api/v1/dashboard/manager`, `GET /api/v1/dashboard/employee` with proper auth
- DTOs for dashboard stats
- Frontend: add `dashboard.model.ts` and a `DashboardService` wired to the endpoints
- Frontend: one shared `DashboardLayoutComponent` (responsive sidebar + content) with role-scoped navigation, used by all three roles
- Frontend: separate `AdminDashboardComponent`, `ManagerDashboardComponent`, `EmployeeDashboardComponent` with role-relevant stats, welcome message, and loading/error/empty states
- Update routing so `/admin`, `/manager`, `/me` load the role-specific components while preserving `authGuard`/`roleGuard` and login/logout
- Component and service tests

## Capabilities

### New Capabilities

- `dashboards`: Role-scoped dashboard statistics and UI for Admin/Manager/Employee

### Modified Capabilities

_None_

## Impact

- Backend: Application/Infrastructure/Api for dashboard endpoints; `DashboardService` stubbed zeros replaced with EF queries
- Frontend: new `core/models/dashboard.model.ts`, `core/services/dashboard.service.ts`, shared `DashboardLayoutComponent`, and `Admin/Manager/EmployeeDashboardComponent`; `app.routes.ts` re-pointed to the role components (still `/admin`, `/manager`, `/me`)
- Preserved: `authGuard`, `roleGuard`, `data.roles`, login redirect, logout, and existing employee/department management
- Tests: frontend service + component specs, updated `app.routes.spec.ts`, backend `DashboardService` tests
- Dependencies: none new
