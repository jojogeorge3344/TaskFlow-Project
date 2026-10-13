# Proposal

## Why

The Employee Management (`/employees`) and Department Management (`/departments`) screens look unfinished. They use Bootstrap class names (`btn`, `table`, `form-control`, `row`, `col-md-*`), but Bootstrap is not installed (`angular.json` only loads `src/styles.css`), so the browser shows them as plain HTML. They also do not render the side menu, so an Administrator who opens them has no navigation and no way to sign out. On top of that, the side menu only collapses on phones and lives inside the dashboard feature. Every screen still to be built (tasks, kanban, reports, settings) needs one shared menu that the user can collapse.

## What Changes

- Add one **application shell**: a header bar plus a side menu that every signed-in screen uses. The shell is moved out of `features/dashboard` into a shared layout with a parent route, so new screens get the menu just by being registered as child routes.
- The side menu gets a **toggle icon button** in the top bar:
  - Desktop/tablet (≥ 992px): the menu switches between expanded (icon + label, 16rem) and collapsed (icon-only rail, 4.5rem). Labels show as tooltips while collapsed. The choice is remembered per browser.
  - Mobile (< 992px): the menu becomes an off-canvas drawer with a dimmed backdrop. It closes on backdrop click, on `Esc`, or after the user picks a link.
- Every menu item gets an **icon**. Screens that are not built yet stay visible but disabled, with a "Soon" label.
- **Redesign Employee Management**: page header with title, subtitle and an "Add employee" primary button; a filter toolbar (search, department, role, status, reset); a data table with avatar initials, role and status badges and row actions; card rows on mobile; loading, empty and error states; an add/edit form in a side panel (drawer) with inline field validation; a read-only **employee details view**; and a confirmation before deactivating.
- **Redesign Department Management**: page header with an "Add department" button; a responsive grid of department cards (name, description, employee count) with Edit, Delete and **"View employees"** actions (opens Employee Management filtered by that department); a client-side search box; add/edit in a modal dialog; and a delete confirmation. Delete stays disabled, with an explanation, while the department has employees.
- Add shared **design tokens and UI primitives** to `styles.css` (colors, spacing, radius, shadows, buttons, inputs, badges, table, card, drawer/modal, toast) so every screen looks the same without adding a CSS framework.
- **BREAKING (internal only):** the three dashboard components stop wrapping themselves in `<app-dashboard-layout>`. The shell route provides the layout instead. URLs do not change.

## Capabilities

### New Capabilities
- `app-shell`: the authenticated application frame: header bar, role-based side navigation with icons, collapse/expand toggle, responsive drawer, remembered state, account area and sign-out.

### Modified Capabilities
- `employee-management`: adds the presentation and interaction requirements for the Employee Management screen (layout, filter toolbar, responsive table, form panel, details view, deactivate confirmation, feedback states). The API behavior does not change.
- `department-management`: adds the presentation and interaction requirements for the Department Management screen (card grid, search, modal form, delete guard and confirmation, "View employees" link). The API behavior does not change.

## Impact

- **Mostly frontend.** Backend changes added during implementation: enums serialized as names (`JsonStringEnumConverter`), only the Administrator is seeded, an optional new password on user update, and a `SecurityStamp` column (migration `AddUserSecurityStamp`) that ends a user's sessions when their username or password changes (see design D8).
- New: `frontend/src/app/core/layout/` (shell component, nav config, icon component), shared UI styles in `frontend/src/styles.css`.
- Changed: `app.routes.ts` (authenticated routes become children of the shell route), the three dashboard component templates (wrapper removed), `features/employees/*`, `features/departments/*`, `dashboard-layout.component.*` (moved or replaced), plus the matching `*.spec.ts` files.
- Employee Management reads an optional `?departmentId=` query parameter so it can be opened pre-filtered.
- No new npm dependencies. Icons are inline SVG and styles are custom CSS (see design.md).
- The in-progress `task-management-impl` change should register its screens as shell child routes and add its menu entries to the shared nav config.
