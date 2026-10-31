# Design

## Context

See proposal.md (Why). Current layout of `frontend/src/app` (Angular 19, standalone components, Karma/Jasmine, no ESLint): 148 source files and 95 specs.

- `core/`: `auth/` (guard, interceptor, service, token store, permissions, role routes), `layout/` (shell, nav items, icon, layout service, and the five `tfa-*` assistant panel components), `models/` (14 files plus an `index.ts` barrel), `services/` (18 API services), `analytics/`, `speech/`, `theme/`, plus `api-base-url*`, `email-domain.ts`, `mobile-number.ts`.
- `features/`: `auth/{login,account-recovery}`, `dashboard/` (+ `analytics/`), `departments`, `employees`, `legal`, `mail`, `notifications`, `onboarding`, `profile`, `projects`, `reports`, `settings`, `tasks` (which also holds `task.service.ts`), `workload`.
- `shared/`: `backdrop`, `charts`, `dashboard-filter-bar`, `employee-picker`, `position-combobox`, `ui/`.
- `app.routes.ts` is a single table, using `loadComponent` for every screen, with `permissionGuard` and route `data`.
- `public/` holds only `favicon.ico`.

## Goals / Non-Goals

**Goals:** every file in its spec-defined place, `ng build` and `ng test` green after each module move, and an automated structure check.

**Non-Goals:** no NgModules, no state library, no path aliases beyond what is needed (see D5), no UI or behaviour changes, no renaming of component classes or selectors.

## Decisions

### D1. Target tree and file mapping

```
src/app/
  modules/
    auth/
      login/              pages/login.component*, (login-reasons, remembered-identifier -> utils/)
      account-recovery/   pages/account-recovery.component*
      security-setup/     pages/security-setup.component*   (from features/onboarding)
      services/           (none: auth API stays in shared/auth, see D3)
    legal/                pages/legal-page.component*, components/legal-footer.component*, models/, legal-documents.ts -> utils/
    dashboard/            pages/{admin,manager,employee}-dashboard.component*, services/dashboard.service, models/dashboard.model
      analytics/          components/* (features/dashboard/analytics), services/{dashboard,manager,employee}-analytics.service,
                          models/analytics.model, utils/{analytics-filter,drill-down} (from core/analytics)
    monitoring/
      task-management/    pages/{task-list,task-create,task-details}.component*, components/{reassign,task-details-tabs css},
                          services/task.service, models/task.model, utils/reminder-options
        discussion/       components/task-discussion.component*
        activity/         components/activity.component*, utils/activity-meta
      board/              pages/kanban.component*
      employee-workload/  pages/workload.component*, services/workload.service, models/workload.model
    management/
      employee-management/ pages/employee-list.component*, components/{employee-details-panel,employee-form-panel}, utils/role-badge,
                           services/user.service, models/user.model
      project-management/  pages/{project-list,project-detail}.component*, components/{project-form-dialog,project-team-dialog},
                           services/project.service, models/project.model
        positions/         services/position.service
      department-management/ pages/department-list.component*, components/{department-form-dialog,department-members-dialog},
                             services/department.service, models/department.model
    communication/
      mail/               pages/mail.component*, components/{mail-list,mail-reader,compose-dialog,recipient-picker},
                          services/mail.service, models/mail.model, utils/mail-format  (mail-test-data -> shared/testing/)
      notification/       pages/notification-center.component*, services/notification.service, models/notification.model
    reports/              pages/report.component*, services/report.service, models/report.model
    settings/
      application-settings/ pages/application-settings.component*, services/application-settings.service
      reminder-settings/    pages/reminder-settings.component*, services/reminder-settings.service
      role-configuration/   pages/permissions-management.component*, utils/permission-tree, services/role.service, models/role.model
      tfa-settings/         pages/tfa-settings.component, components/ai-provider-settings.component*, services/ai-provider-admin.service
      models/settings.model.ts  (shared by the settings sub-modules)
    profile/              pages/{profile,change-password}.component*, services/profile.service, profile-shared.css -> pages/
    assistant/            components/tfa-{panel,header,composer,message,action-card}.component*, services/{assistant,speak,wake-phrase}.service,
                          models/assistant.model
  shared/
    components/   backdrop/, charts/, dashboard-filter-bar/, employee-picker/, position-combobox/, confirm-dialog, drawer, toast-host, icon
    directives/   email-local-part.directive
    services/     toast.service, theme.service
    utils/        api-error, initials, linkify, local-day, return-url, save-blob, allocation-format, email-domain, mobile-number
    auth/         auth.guard, auth.interceptor, auth.service, auth.models, token.store, permissions, role-routes
    testing/      (from src/app/testing)
  layouts/        shell.component*, nav-items, layout.service, theme-menu.component*
  routes/         app.routes.ts (+ specs)
  config/         app.config.ts, api-base-url.ts, api-base-url.interceptor.ts
  app.component.*  (root component stays at src/app)
src/assets/       favicon.ico (from public/)
```

`dashboard-filter-bar`, `employee-picker` and `position-combobox` are used by two or more modules, so they stay shared. The `*.spec.ts` files move next to the file they test.

`models/` and `utils/` are allowed inside a module alongside `pages/`, `components/` and `services/`. REQ-05's three folders are the minimum, and types and pure helpers need a home that is not `services/`.

### D2. Per-module route files
Each top-level module exports `<module>.routes.ts` (e.g. `MONITORING_ROUTES`) containing the same path entries, guards and `data` that `app.routes.ts` has today. `routes/app.routes.ts` keeps the public routes and the shell route, and its children become `{ path: '', loadChildren: () => import('../modules/monitoring/monitoring.routes').then(m => m.MONITORING_ROUTES) }`. Because Angular merges empty-path `loadChildren` children into the parent, the URLs don't change. `app.routes.spec.ts` asserts the flattened table, which proves it.
- *Alternative considered:* keep one route file and only update import paths. That is simpler, but it puts every module's routing in one shared file, against the per-module goal.

### D3. Auth plumbing lives in `shared/auth/`
`auth.service`, the guards, the interceptor and the token store are used by every module and by the app config, so they go in `shared/auth/`. The login and recovery pages live in `modules/auth/` and import from `shared/auth/`. This exception for API services is named explicitly in spec "API service placement".

### D4. Moving files without breaking imports
Move with `git mv` so history is kept. Rewrite relative imports with a small Node script (`scripts/move-frontend.mjs`, deleted after use) that takes an old-path → new-path map, moves the files and rewrites every `import ... from './…'` that resolves to a moved file. Run `ng build` after each module batch. The `core/models/index.ts` barrel is removed, and imports point at the concrete model file.
- *Alternative considered:* moving by hand in the IDE. That is error-prone across ~240 files, and an IDE move doesn't update template `styleUrls` reliably.

### D5. No new path aliases
Relative imports stay. A `@shared/*` alias would shorten imports but changes every import line again and adds `tsconfig` and Karma config. It can be revisited later without changing this spec.

### D6. Structure check
`scripts/check-frontend-structure.mjs` (Node, no dependencies) walks `src/app` and enforces each `frontend-architecture` rule:
- allowed top-level folders;
- module folders appear in the allowed module list, which is kept in the script and mirrors design D1;
- files directly in a module folder may only be `<module>.routes.ts`;
- the suffix matches the folder (`models/` → `*.model.ts` or `*.models.ts`; `utils/` → any plain `.ts`; `.css`/`.html` files sit beside their component);
- no empty directories;
- `core/` and `features/` are absent;
- any `.ts` file that injects `HttpClient` is under `services/` or `shared/auth/`.

It runs as `npm run check:structure` and also inside `npm test` (`"test": "npm run check:structure && ng test"`).

### D7. Assets
Move `public/favicon.ico` to `src/assets/` and change both `angular.json` asset entries (build and test) to `{ "glob": "**/*", "input": "src/assets" }`. `index.html` references `favicon.ico` at the root, and the glob output stays at the root, so the URL doesn't change.

## Risks / Trade-offs

- [Broken relative imports, CSS `styleUrls` or `templateUrl` after moves] → Use the scripted rewrite, and run `ng build` after each module batch. Templates and styles move together with their component.
- [Route order changes when split into `loadChildren`, so a wildcard or redirect matches earlier] → Keep the module route files in the same order as today's children, and `app.routes.spec.ts` asserts the full flattened table.
- [Conflicts with the backend change on `login.component.ts`] → It's a one-line edit. Whichever change merges second rebases it.
- [Nav item paths or `role-routes.ts` hold string URLs, not imports] → URLs don't change, so nothing to do. The route spec verifies the URLs.

## Migration Plan

Do one module batch per commit, each followed by `ng build`, `ng test` and `npm run check:structure` (warnings-only until the last batch). Deploy to the Render dev frontend at the end and smoke-test navigation for all three roles. To roll back, revert the commits. There is no data or API impact.

## Implementation notes

Where the code differs from the D1 table, because the "shared only when shared" rule or the route rule decided it:

- `drill-down` and `analytics-filter` are in `shared/utils/` (the task list uses them as well as the dashboards), as are
  `role-badge`, `reminder-options`, `chart-colors`, `chart-providers` and `backdrop-animation`.
- `legal-footer` is in `shared/components/` (the shell and the auth pages use it). `login-reasons` stays in
  `shared/auth/` (the interceptor, profile and employee screens use it).
- `dashboard-filter-bar` is in `modules/dashboard/components/` and `employee-picker` in
  `modules/management/project-management/components/`, each used by one module only. `position-combobox` is used by two
  management sub-modules, so it is in `modules/management/components/`.
- Task discussion, activity and reassign are routed, so they are in `pages/`.
- Each shared component has a folder of its own under `shared/components/` (the charts share `charts/`).
- A component's or service's own types are in a `<name>.model.ts` beside it (spec "Consistent file naming").
- Splitting the route table by module groups Monitoring's routes together, so the shell's child order changed while
  every path, guard and `data` value stayed the same. `app.routes.spec.ts` compares the expanded table without
  depending on module order, and keeps the `tasks/new` before `tasks/:id` check.
