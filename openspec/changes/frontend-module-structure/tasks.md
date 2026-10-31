# Tasks

## 0. Prerequisite

- [ ] 0.1 Finish or archive the in-progress OpenSpec changes that still plan frontend edits, and create branch `frontend-module-structure` from up-to-date `master`. Record the baseline: run `ng test` and note the passing spec count, and confirm `ng build` succeeds

## 1. Tooling

- [x] 1.1 Add `scripts/check-frontend-structure.mjs` implementing every rule in design D6, with a `--warn` flag, and add the `check:structure` npm script. Verify that it runs in warn mode and lists today's `core/` and `features/` files as violations
- [x] 1.2 Add the temporary `scripts/move-frontend.mjs` (`git mv` plus relative-import rewrite from a path map, design D4). Verify it with a dry run on one file that prints the rewritten import lines

## 2. App-level folders

- [x] 2.1 Move `app.config.ts`, `api-base-url.ts` and `api-base-url.interceptor.ts` to `config/`. Move `app.routes.ts` and its two specs to `routes/`. Update `main.ts`, and verify that `ng build` and `ng test` pass
- [x] 2.2 Move the shell, nav items, layout service and theme menu to `layouts/`, and `icon.component` to `shared/components/`. Verify that `ng build` and `ng test` pass
- [x] 2.3 Move `core/auth/*` to `shared/auth/`, `shared/ui/*` and the other shared widgets to `shared/{components,directives,services,utils}`, `theme.service` to `shared/services`, `email-domain`/`mobile-number` to `shared/utils`, and `src/app/testing` to `shared/testing`. Verify that `ng build` and `ng test` pass
- [x] 2.4 Move `public/favicon.ico` to `src/assets/` and update both `angular.json` asset inputs. Verify that `ng build` output contains `favicon.ico` at the root

## 3. Modules: auth, legal, profile

- [x] 3.1 Move login, account recovery and security setup into `modules/auth/<sub>/pages` (helpers to `utils/`), and create `modules/auth/auth.routes.ts`. Verify that `ng test` passes, including `app.routes.recovery.spec.ts`
- [x] 3.2 Move legal into `modules/legal/{pages,components,models,utils}` and profile into `modules/profile/{pages,services}`, with route files. Verify that `ng build` and `ng test` pass

## 4. Module: dashboard

- [x] 4.1 Move the dashboards to `modules/dashboard/pages`, analytics to `modules/dashboard/analytics/{components,services,models,utils}` (including `core/analytics`), and `dashboard.service`/`dashboard.model`. Create `dashboard.routes.ts`. Verify that `ng build` and `ng test` pass

## 5. Module: monitoring

- [x] 5.1 Move tasks into `modules/monitoring/task-management/` with the `discussion/` and `activity/` sections, `task.service`, `task.model` and `reminder-options`. Move kanban to `board/pages` and workload to `employee-workload/`. Create `monitoring.routes.ts`. Verify that `ng build` and `ng test` pass

## 6. Module: management

- [x] 6.1 Move employees, projects (+ `positions/services`) and departments into `modules/management/<sub>/`, with `user`/`project`/`position`/`department` services and models. Create `management.routes.ts`. Verify that `ng build` and `ng test` pass

## 7. Module: communication

- [x] 7.1 Move mail and notifications into `modules/communication/{mail,notification}/`, with their services and models, and `mail-test-data` to `shared/testing`. Create `communication.routes.ts`. Verify that `ng build` and `ng test` pass

## 8. Modules: reports, settings, assistant

- [x] 8.1 Move reports into `modules/reports/`, and settings into `modules/settings/{application-settings,reminder-settings,role-configuration,tfa-settings}/` with `settings.model` in `modules/settings/models`. Create the route files. Verify that `ng build` and `ng test` pass
- [x] 8.2 Move the `tfa-*` panel components, `assistant.service`, the speech services and `assistant.model` into `modules/assistant/`, and update the shell's imports. Verify that `ng build` and `ng test` pass

## 9. Lock down

- [x] 9.1 Delete the empty `core/` and `features/` folders, `core/models/index.ts` and `scripts/move-frontend.mjs`. Switch `check:structure` to strict and chain it into `npm test`. Verify that `npm test` passes with the baseline spec count from 0.1 and that `npm run check:structure` reports zero violations
- [x] 9.2 Verify that `app.routes.spec.ts` asserts the full flattened route table (paths, guards, `data`) and that it matches the baseline. Add assertions if it does not
- [ ] 9.3 Update the frontend section of `README.md` with the module tree and the placement rules. Deploy to the Render dev frontend, and smoke-test navigation and one action per module as Administrator, Manager and Employee

## Workflow follow-up

- Raise the PR against `master`, review, merge.
- Archive the change with `/opsx:archive` once merged.
