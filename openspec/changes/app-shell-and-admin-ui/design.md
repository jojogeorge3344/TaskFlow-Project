# Design

## Context

- The frontend is Angular 19 with standalone components, signals for state, and lazy-loaded feature routes.
- `project.md` lists Bootstrap 5 in the stack, but it was never installed. `angular.json` only loads `src/styles.css`. The employee and department templates use Bootstrap class names, so they render as unstyled HTML.
- The dashboards already have a custom design (slate and blue palette, `stat-card`, `.dashboard__*` in `styles.css`) and a `DashboardLayoutComponent` that each dashboard wraps itself in with `<app-dashboard-layout>`. That layout:
  - only toggles on narrow screens (no desktop collapse),
  - uses text-only nav items,
  - lives in `features/dashboard`, so other features would have to import from a sibling feature.
- `/employees` and `/departments` do not use the layout at all.
- The in-progress `task-management-impl` change (task 4.4, "Routes") will add more authenticated screens soon.

Visual reference: `mockup.html` in this change folder, also published at https://claude.ai/artifact/TrFQcfq6KJpP65P9tH15VQ (shell expanded, collapsed and mobile; Employee Management; Department Management).

## Goals / Non-Goals

**Goals:**
- One shell, applied through routing, so feature components never wrap themselves.
- A single source of truth for navigation items and their icons, per role.
- Shared CSS tokens and primitives that the redesigned screens use, and that later screens can reuse.
- Responsive from 360px to wide desktop, keyboard operable, WCAG AA contrast.

**Non-Goals:**
- No backend or API changes, no server-side pagination (lists are small; the user list endpoint returns an array today).
- No dark mode, no theming switch.
- No restyling of the login screen. The dashboards get the shell but no other visual changes beyond removing the wrapper.
- No building of placeholder screens (tasks, reports, and so on). They stay "Soon" items.

## Decisions

### D1. Shell as a parent route (`ShellComponent` + `<router-outlet>`)

Create `core/layout/shell.component.*` and make every authenticated route a child of one componentless-path parent route:

```ts
{ path: '', component: ShellComponent, canActivate: [authGuard], children: [
    { path: 'admin', ... }, { path: 'employees', ... }, { path: 'departments', ... }, ...
]}
```

`roleGuard` stays on each child. The dashboards drop their `<app-dashboard-layout>` wrapper. `DashboardLayoutComponent` is deleted, and its spec moves to `shell.component.spec.ts`.

- *Why:* new screens get the menu just by being registered, the shell is not re-created on navigation (the collapse state and scroll stay stable), and it removes the feature→feature import.
- *Alternative:* keep the content-projection wrapper and add it to employees and departments. Rejected because every future screen would have to remember to add it, and the shell would be torn down on every navigation.

### D2. Collapse state model

Two signals in a small `LayoutService` (`core/layout/layout.service.ts`):
- `collapsed` (desktop rail): persisted under `localStorage['tf.sidebar.collapsed']`, with read and write wrapped in try/catch and a default of `false`.
- `mobileOpen` (drawer): never persisted, and reset on every `NavigationEnd`.

No `BreakpointObserver` is needed: a `matchMedia('(min-width: 992px)')` listener feeds an `isWide` signal. The toggle button calls `isWide() ? toggleCollapsed() : toggleMobile()`. `aria-expanded` is bound to `isWide() ? !collapsed() : mobileOpen()`.

- *Why 992px:* below it, a 16rem menu plus a data table does not fit. It matches Bootstrap's `lg` breakpoint, in case Bootstrap is adopted later.
- *Alternative:* `@angular/cdk/layout`. The CDK is planned for kanban, but it is not installed yet, and a single `matchMedia` call is enough.

### D3. Icons: inline SVG through an `IconComponent`

Add `core/layout/icon.component.ts` (`<tf-icon name="users" />`). It holds a small map of about 20 stroke icons (24px viewBox, `currentColor`, 1.75 stroke, Lucide-style paths copied in under the ISC license). Nav config entries get an `icon` key.

- *Why:* `agent.md` forbids adding libraries without checking the stack. Inline SVG needs no dependency, works offline, and inherits the text color.
- *Alternative:* the Bootstrap Icons font. It would add a dependency plus a font request for 20 glyphs.

### D4. Styling: custom CSS tokens and primitives, no Bootstrap install

Extend `src/styles.css` with:
- `:root` tokens: `--tf-primary #2563eb`, `--tf-sidebar #0f172a`, `--tf-surface #fff`, `--tf-bg #f1f5f9`, `--tf-border #e2e8f0`, `--tf-text #0f172a`, `--tf-muted #64748b`, `--tf-success #16a34a`, `--tf-danger #dc2626`, `--tf-warning #d97706`, `--tf-radius .625rem`, shadows, a 4px spacing scale, and `--tf-sidebar-w 16rem` / `--tf-rail-w 4.5rem`. These match the existing dashboard colors.
- Primitives: `.tf-btn` (`--primary`, `--ghost`, `--danger`, `--icon`, `--sm`), `.tf-input` / `.tf-select` / `.tf-field` (+ error text), `.tf-badge` (role and status variants), `.tf-card`, `.tf-table` (sticky header, row hover), `.tf-page-header`, `.tf-toolbar`, `.tf-empty`, `.tf-skeleton`, `.tf-drawer` / `.tf-modal` / `.tf-backdrop`, and `.tf-toast`.

Remove all Bootstrap class names from the two templates. Component CSS files hold only layout that is specific to that screen (grid templates, the card-row transform for the table).

- *Why:* it matches what is actually loaded and what the dashboards already use, and keeps the bundle small.
- *Alternative:* `npm i bootstrap` and keep the classes. That is viable, but mixing it with the dashboards' custom styles gives two visual languages and a heavier bundle. Recorded so the TDD and `project.md` can be updated to "custom CSS tokens" (or Bootstrap formally dropped from the stack) in this change.

### D5. Overlays: drawer for employee form/details, modal for department form and confirmations

- `EmployeeFormPanelComponent` (Reactive Forms: `FormGroup` with `Validators.required`/`email`; the password control is added only in create mode) and `EmployeeDetailsPanelComponent`. Both are rendered in a shared `.tf-drawer` (right side, 28rem, full-width under 576px).
- `DepartmentFormDialogComponent` and a reusable `ConfirmDialogComponent` (`shared/ui/confirm-dialog`), built on the native `<dialog>` element with `showModal()`. This gives a focus trap, `Esc` handling, and the backdrop for free.
- Toasts: `shared/ui/toast.service.ts` (a signal array, auto-dismiss after 4s) plus `<tf-toast-host>` rendered once in the shell.
- *Why Reactive Forms:* inline validation per field. The existing template-driven `ngModel` fields have no validation messages. Server errors still come from `err.error?.detail ?? err.error?.error` (ProblemDetails first).
- *Why the native `<dialog>`:* there is no CDK yet, and it is accessible by default.

### D6. Employee list behavior

- Filters are held in signals. Search goes through a `Subject` with `debounceTime(300)` and `distinctUntilChanged`. Select filters trigger the load immediately. Each new load cancels the previous request (`switchMap`).
- `departmentId` is read once from `ActivatedRoute.queryParamMap`, and later filter changes update the URL with `router.navigate([], { queryParams, replaceUrl: true })`. This makes the department filter shareable and lets the back button work.
- Responsive table: one `<table>`. Below 768px, CSS turns rows into cards (`display:block`, cell `data-label` pseudo-labels). There is no second template to keep in sync.
- Deactivate goes through `ConfirmDialogComponent`. Activate does not.

### D7. Department list behavior

Load once, and filter client-side with a `computed()` over the `departments` and `query` signals. "View employees" uses `routerLink="/employees" [queryParams]="{ departmentId: d.id }"`. Delete is disabled when `employeeCount > 0`, with a `title` tooltip and an `aria-describedby` hint.

## Risks / Trade-offs

- [Moving the dashboards under a parent route breaks their existing specs, which expect the wrapper] → update those specs in the same task group, and port the layout spec cases (role menus, toggle, sign-out) to the shell spec.
- [`localStorage` blocked (private mode)] → try/catch, default expanded. Persistence is a convenience only.
- [The CSS card transform for the table is less flexible than a separate mobile template] → acceptable for six columns. Revisit if the task list needs a richer mobile layout.
- [Native `<dialog>` styling differs slightly across browsers] → reset its default styles in `.tf-modal`. All evergreen browsers support `showModal()`.
- [`task-management-impl` is in progress and may add routes the old way] → its task 4.4 should register screens as shell children and add entries to `NAV_ITEMS`. This is noted in the proposal's Impact section.

## Migration Plan

Frontend-only and deployed with the SPA. Rollback means reverting the commit. No data or API migration.
