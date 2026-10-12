## Context

Extend existing auth and domain (users/departments) to add role-scoped dashboards. Need DashboardService + API + frontend UI.

## Goals / Non-Goals

**Goals:** Role-scoped stats endpoints and UI components for admin/manager/employee dashboards, one shared responsive layout with role-scoped navigation, real (non-fabricated) statistics sourced from the backend, and component/service tests.
**Non-Goals:** Full task management features beyond dashboard stats; reports export beyond what's needed; charts library setup beyond existing (Chart.js/ng2-charts already in stack per TDD); inventing backend metrics that the domain model cannot yet produce.

## Decisions

| Decision | Rationale | Alternatives considered |
|---|---|---|
| D1: DashboardService in Application layer with interface, impl in Infrastructure | Consistent with existing service pattern | Direct EF in controllers - breaks layering |
| D2: Three separate endpoints returning typed DTOs | Clear role separation, easy to consume by role-specific components | Single generic endpoint - less type-safe |
| D3: Frontend uses role-specific lazy components for /admin, /manager, /me | Keeps routing simple while allowing custom UI per role | Single component switching by role - works but less modular |
| D4: One shared `DashboardLayoutComponent` (responsive sidebar + content outlet) wraps all three role dashboards; only the nav menu is role-specific | Single shell to build/maintain, consistent responsive behaviour, no duplicated chrome | Per-role sidebars inside each component (duplication); three separate shells (more files, drift) |
| D5: Components named `AdminDashboardComponent`, `ManagerDashboardComponent`, `EmployeeDashboardComponent` loaded at `/admin`, `/manager`, `/me` | Matches TDD §Routing and requirement naming; removes ambiguity in "create/update if needed" | Reuse the current generic placeholder - does not satisfy role-specific requirements |
| D6: Statistics sourced from `GET /api/v1/dashboard/*` via a frontend `DashboardService`; backend zero-stubs replaced with EF queries on the existing `Task`/`TaskRecipient` model | Requirement forbids hardcoded/fabricated stats; the domain entities already exist | Keep hardcoded zeros - violates "do not fabricate"; leave endpoints unused - dashboard is meaningless |
| D7: Sidebar links are data-driven from `AuthService.currentUser().role` and only point at routes that exist | Avoids dead links and duplicated role logic | Hardcode all links in template - leaks role rules into markup and links to non-existent routes |

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| N+1 queries in dashboard aggregates | Use single queries with grouping/counts where possible |
| Performance with large datasets | Indexes exist on key columns (users, tasks status/deadline); counts are cheap |
| Manager scope unclear without tasks implemented | Manager stats now derive from the `Task`/`TaskRecipient` model where possible; where a metric is genuinely underivable, document it and show it as unavailable rather than a fabricated zero |
| Backend stats incomplete or misleading | Task 2.5 replaces zero-stubs with real queries; 2.6 covers the queries with tests; the final summary lists any metric still missing |
| Responsive sidebar complexity / layout drift across roles | Single shared layout component keeps one source of truth for responsive behaviour; role differences limited to the nav config |
| Role navigation pointing at non-existent routes | D7 restricts links to routes that actually exist in `app.routes.ts` |
| Regressions to auth/guards while re-pointing routes | Route guards (`authGuard`, `roleGuard`, `data.roles`) are preserved unchanged; `app.routes.spec.ts` updated to keep asserting them |

## Migration Plan

1. Add DTOs in Application/Dashboard
2. Define IDashboardService
3. Implement DashboardService in Infrastructure (basic counts)
4. Add DashboardController with 3 endpoints
5. Register service in DI
6. Replace hardcoded zero statistics with real EF queries on `Task`/`TaskRecipient`; document any metric still unavailable
7. Add frontend `dashboard.model.ts` and `DashboardService` wired to the endpoints
8. Add the shared `DashboardLayoutComponent` (responsive sidebar, role-scoped nav, sign out)
9. Add `AdminDashboardComponent`, `ManagerDashboardComponent`, `EmployeeDashboardComponent` with stats, loading, error and empty states
10. Re-point `/admin`, `/manager`, `/me` to the new components while preserving `authGuard`/`roleGuard` and `data.roles`
11. Add unit/component/integration tests; run `npm run build`, `npm run test`, `dotnet test`
12. Manual smoke per role (correct dashboard, cross-role access denied, login/logout intact)
