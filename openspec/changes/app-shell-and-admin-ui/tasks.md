# Tasks

## 1. Design tokens and UI primitives

- [ ] 1.1 Add `:root` design tokens and base element styles to `frontend/src/styles.css` (D4), then switch the existing dashboard and stat-card rules to use the tokens. Verify with `npm run build` and confirm the dashboards look unchanged.
- [x] 1.2 Add the primitives `.tf-btn*`, `.tf-input`/`.tf-select`/`.tf-field`, `.tf-badge*`, `.tf-card`, `.tf-table`, `.tf-page-header`, `.tf-toolbar`, `.tf-empty`, `.tf-skeleton`, `.tf-drawer`, `.tf-modal`, `.tf-backdrop` and `.tf-toast` to `styles.css`, including focus-visible rings and the 768px/576px responsive rules. Verify the build passes.

## 2. Application shell

- [x] 2.1 Create `core/layout/icon.component.ts` with the inline SVG icon map (D3). Verify with a spec that renders a known icon and leaves an unknown name empty.
- [x] 2.2 Create `core/layout/nav-items.ts`. Move `ROLE_NAVIGATION` there with an `icon` key per item. Verify the build passes.
- [x] 2.3 Create `core/layout/layout.service.ts` with `collapsed` (persisted, try/catch, default `false`), `mobileOpen` and `isWide` (matchMedia 992px) (D2). Verify with specs covering persistence round-trip, blocked storage, and the toggle chooses rail vs drawer mode depending on viewport width.
- [x] 2.4 Create `core/layout/shell.component.*`: header bar with the toggle icon button (`aria-label`, `aria-expanded`), sidebar (brand, icon nav with `routerLinkActive` + `aria-current`, "Soon" disabled items, tooltips while collapsed), account area (initials avatar, name, role, sign-out), mobile backdrop, Esc-to-close with focus returned to the toggle, and close on `NavigationEnd`. Verify with `shell.component.spec.ts`, which ports the cases from `dashboard-layout.component.spec.ts` and adds collapse, aria-expanded, and drawer-close cases.
- [x] 2.5 Create `shared/ui/toast.service.ts` and `<tf-toast-host>`, and render the host in the shell. Verify with a spec showing a toast appears and auto-dismisses.

## 3. Routing migration

- [x] 3.1 Restructure `app.routes.ts` so all authenticated routes are children of `ShellComponent` with `authGuard` on the parent and `roleGuard` on each child (D1). Verify `app.routes.spec.ts` is updated and passes, and that `/login` still renders without the shell.
- [ ] 3.2 Remove the `<app-dashboard-layout>` wrapper from the admin, manager and employee dashboard templates and imports, then delete `features/dashboard/dashboard-layout.component.*`. Verify the dashboard specs pass and each dashboard shows inside the shell in `npm start`.

## 4. Shared overlays

- [x] 4.1 Create `shared/ui/confirm-dialog` on top of the native `<dialog>` (title, message, confirm label and variant, returns a result). Verify with a spec covering confirm and cancel.
- [x] 4.2 Add the right-side drawer host pattern (`.tf-drawer` + backdrop, Esc/backdrop close, focus moves to the first field on open). Verify by testing it through the employee panel specs in 5.3.

## 5. Employee Management redesign

- [ ] 5.1 Rewrite `employee-list.component.html/.css` with the page header (title, count subtitle, "Add employee"), the filter toolbar (search, department, role, status, Reset) and the `.tf-table` with avatar, badges, "Unassigned" fallback and row actions. Add the <768px card-row transform. Verify at 390px, 768px and 1280px with no horizontal scroll.
- [x] 5.2 Refactor `employee-list.component.ts`: filter signals, 300ms debounced search, select filters applied on change, `switchMap` loading, `departmentId` read from and written to query params, and loading/empty/error states with Retry and Clear filters (D6). Verify by creating `employee-list.component.spec.ts` (it does not exist yet) covering debounce, query-param prefilter, empty state and retry.
- [x] 5.3 Create `employee-form-panel` (Reactive Forms, optional employee code field, inline errors, password and status fields only in create mode, server error shown in the panel, Save disabled while saving) and `employee-details-panel` (read-only fields + Edit). Verify with specs for create-mode vs edit-mode fields, required/email errors, a kept-open panel on a 409 error, and the success toast.
- [x] 5.4 Wire Deactivate through `ConfirmDialogComponent` (no confirmation for Activate) and show toasts. Verify with a spec showing a cancelled confirmation does not call `updateStatus`.

## 6. Department Management redesign

- [ ] 6.1 Rewrite `department-list.component.html/.css`: page header with count and "Add department", search field, and a card grid (1 column / 2 columns at ≥576px / auto-fill at ≥1200px) with name, description fallback, employee count, Edit, guarded Delete with tooltip, and View employees (`/employees?departmentId=`). Verify at 390px, 768px and 1280px.
- [x] 6.2 Refactor `department-list.component.ts`: client-side `computed()` search, loading/empty/no-match/error states, and replace `confirm()` with `ConfirmDialogComponent` (D7). Verify by creating `department-list.component.spec.ts` (it does not exist yet) covering search filtering, the disabled delete when employees > 0, and the confirm flow.
- [x] 6.3 Create `department-form-dialog` (native `<dialog>`, required Name, server error kept inside the dialog, toast on success). Verify with a spec for the required-name error and a duplicate-name error that keeps the dialog open.

## 7. Documentation

- [x] 7.1 Update `TaskFlow Proj/project.md` (and the TDD frontend section) to say styling uses custom CSS tokens and primitives in `styles.css` instead of Bootstrap, and that authenticated screens register as children of the shell route with their nav entry in `core/layout/nav-items.ts`. Verify the docs no longer claim Bootstrap is installed.

## 8. Integration verification

- [x] 8.1 Run `npm run build` and `npm test` (`ng test --watch=false`) in `frontend/`. Both must pass with no new warnings.
- [ ] 8.2 Manual check in `npm start` as Administrator, Manager and Employee: role menus and icons, active highlight, desktop collapse persisted across reload, mobile drawer (390px) closing on link/backdrop/Esc, sign-out from `/employees`, the full employee CRUD and deactivate flow, the department CRUD flow, and View employees prefilter. Compare against `mockup.html`.

## 9. Credential changes end sessions (added during implementation)

- [x] 9.1 Serialize enums as names in the API (`JsonStringEnumConverter`) so the SPA's role strings round-trip, and seed only the Administrator. Verify with `UsersEndpointsTests` and the updated `DatabaseSeederTests`.
- [x] 9.2 Add `User.SecurityStamp` (migration `AddUserSecurityStamp`), the `sstamp` token claim, and stamp validation in `OnTokenValidated`; accept an optional new password on update, and on a username or password change issue a new stamp and revoke refresh tokens (D8). Verify with integration tests for password change, username change, a non-credential edit, and a too-short password.
- [x] 9.3 In the SPA: optional "New password" when editing, immediate sign-out when the Administrator changes their own credentials, a login notice for `reason=session-ended|credentials-changed`, and a session re-check every 60s and on tab focus. Verify with the form-panel, shell, interceptor and login specs.
