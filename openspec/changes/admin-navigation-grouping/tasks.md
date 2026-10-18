# Tasks

## 1. Navigation model

- [x] 1.1 Add an optional `children?: NavItem[]` field to the `NavItem` interface in `frontend/src/app/core/layout/nav-items.ts` so a navigation entry can be a parent module. Verify with `npm run build` in `frontend` passing.

## 2. Administrator navigation grouping

- [x] 2.1 Restructure the `Administrator` entry of `ROLE_NAVIGATION` in `nav-items.ts`: add a `Settings` parent (icon `settings`) with children `Reminder Settings` (`/settings/reminders`, icon `clock`) and `Application Settings` (`/settings/application`, icon `settings`); a `Communication Management` parent (icon `mail`) with children `Mail` (`/mail`, icon `mail`, keeps `badge: 'mail'`) and `Notifications` (`/notifications`, icon `bell`); and a `TaskFlow Management` parent (icon `tasks`) with children `Employee Management` (`/employees`, icon `users`) and `Department Management` (`/departments`, icon `building`), while `Dashboard`, `Task Management`, `Employee Workload`, `Reports` and `My Profile` stay top-level. Verify with `npm run build` and by asserting in a spec (extend `nav-items` coverage or `shell.component.spec.ts`) that these three parents exist with exactly the listed children and routes.

## 3. Shell rendering

- [x] 3.1 Update `shell.component.html` (and `.css` as needed) to render parent nav items as expandable groups whose children are listed beneath them, preserving the existing `routerLink`/`routerLinkActive`/`aria-current` behavior for child items, the `mail` badge on the Mail child, and the collapsed-rail tooltips. Verify with updated `shell.component.spec.ts` cases: a parent module renders its children, clicking a child navigates to its route, and the Mail badge still shows the unread count.

- [x] 3.2 Make the header `pageTitle` computed in `shell.component.ts` resolve to the active child's label (searching both top-level items and their children) so the header shows e.g. "Reminder Settings" instead of the parent label. Verify with a spec assertion on `pageTitle` for a route matching a grouped child.

## 4. Verification

- [x] 4.1 Run `npm test` and `npm run build` in `frontend` and confirm all suites pass with no new failures versus the pre-change baseline; manually confirm the Administrator sidebar shows the three parent modules with their sub-modules and that each sub-module still routes to its existing screen behind the unchanged role guards.

## 5. Sidebar grouping refinements (all roles)

- [x] 5.1 Restructure the Administrator nav in `nav-items.ts`: add a `TaskFlow Monitoring` parent (icon `tasks`) containing Task Management (`/tasks`), Employee Workload (`/workload`) and Board (`/board`, icon `kanban`); remove those two now-grouped top-level entries; final order Dashboard, TaskFlow Monitoring, TaskFlow Management, Communication Management, Reports, Settings, My Profile. Verify by updating `nav-items.spec.ts` to assert the new parents, children and order.

- [x] 5.2 Restructure the Manager nav: add `Communication Management` (Mail `/mail` with `badge: 'mail'`, Notifications `/notifications`), `TaskFlow Monitoring` (Task Management `/tasks`, Employee Workload `/workload`, Board `/board`), and `Settings` (Reminder Settings `/settings/reminders`); keep Dashboard, Reports and My Profile top-level. Verify with `nav-items.spec.ts` assertions for the Manager structure.

- [x] 5.3 Restructure the Employee nav: add `Communication Management` (Mail with badge, Notifications) and a `Task Management` parent (My Tasks `/tasks`, Board `/board`); keep Dashboard and My Profile top-level. Verify with `nav-items.spec.ts` assertions for the Employee structure, and re-run `shell.component.spec.ts` (update role nav expectations) plus `npm run build`.

## 6. Board screen with filters

- [x] 6.1 Add a filter bar to `KanbanComponent`: Assignee dropdown (populated from the users service, visible for Administrator and Manager only), Status dropdown covering all statuses, Deadline from/to date inputs, a Clear filters action, and a filtered-empty state distinct from the load-error state. Filters apply client-side (AND semantics) to the already-loaded board list; `/board` route and guards unchanged. Verify with `kanban.component.spec.ts` cases: each filter narrows the columns, combined filters AND together, Clear resets, empty state renders, Employee sees no assignee filter, and drag-drop still commits status changes on a filtered board.

- [x] 6.2 Board layout: the wide-screen grid must show all five status columns (To Do, In Progress, In QA, Blocked, Completed) in one row - update `kanban.component.css` from the stale four-column template to five. Verify with `npm run build` (and visual spot-check).

## 7. In-QA task status

- [x] 7.1 Backend: append `InQA` after `Cancelled` in `TaskFlow.Domain.Enums.TaskStatus`; extend `TaskStatusTransitions` with InProgress→InQA, InQA→InProgress, InQA→Completed, InQA→Blocked, InQA→Cancelled (To Do→In-QA stays invalid; Completed/Cancelled terminal). Update/extend the TDD transition-table tests in `TaskTrackingDomainTests`. Verify with `dotnet test`.

- [x] 7.2 Backend: sweep every status `switch`/`switch` expression (stage calculator, report/workload/dashboard services, validators) so In-QA is treated as an active/open status; add unit coverage for at least one workload/report case containing an In-QA task. Verify with `dotnet test`.

- [x] 7.3 Frontend: add `InQA` to `TASK_STATUSES` and `BOARD_STATUSES` (after `InProgress`); add label `'In QA'` to the kanban and task-list status label maps; update `KanbanComponent` column order to ToDo, In Progress, In QA, Blocked, Completed; extend `task-details` `allowedNext` (InProgress adds `'InQA'`; add an In-QA case returning `['InProgress', 'Blocked', 'Completed', 'Cancelled']`). Verify with `npm test` (task-list, task-details, kanban specs updated for In-QA) and `npm run build`.

- [x] 7.4 Verify In-QA flows into task create/edit forms and the Reports status filter (both driven by `TASK_STATUSES`) with at least one spec assertion each where such specs exist. Verify with `npm test`.

## 8. Sidebar UX fixes

- [x] 8.1 Fix parent-label truncation: in the expanded sidebar a parent label such as "Communication Management" must wrap onto multiple lines (e.g. "Management" below "Communication") instead of being cut off; the collapsed-rail tooltip keeps the single-line label. Verify with a `shell.component.spec.ts` assertion that the parent label element allows wrapping (and CSS review via `npm run build`).

- [x] 8.2 Fix the collapse defect: remove the force-open behavior in `isGroupOpen` so a parent is always collapsible even when a child is active; instead expand the group containing the newly active route on each `NavigationEnd`, and let subsequent manual toggles win until the next navigation. Verify with `shell.component.spec.ts` cases: clicking the active group's parent collapses it and it stays collapsed; navigating to a child expands its parent; a manual collapse after navigation is not overridden.

- [x] 8.3 Parent modules start minimized on login: flip the `isGroupOpen` default so every group renders collapsed when the shell loads (only the group containing a child active route is pre-expanded via `expandGroupContaining` in the constructor and on `NavigationEnd`). Update `shell.component.spec.ts` (groups start `aria-expanded="false"`, child links appear only after expand) and the `app-shell` delta spec. Verify with `npm test`.

## 9. Verification

- [x] 9.1 Run `npm test`, `npm run build` in `frontend` and `dotnet test` in `backend`; confirm all suites pass. Manually spot-check all three sidebars (grouping, two-line wrap, collapse), the Board filters (assignee/status/deadline, combined and cleared), and the In-QA flow (move to In-QA, send back, complete) against the unchanged routes and guards.

## Workflow follow-up

- Archive the change after review so the deltas sync into the main `app-shell`, `board-view` and `task-status` specs.
