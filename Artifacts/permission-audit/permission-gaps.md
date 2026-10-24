# TaskFlow permission gaps (discovery only)

Basis: static read of branch `Develop_env`. Nothing was changed, and no live database rows or running API were inspected. Items marked **existing** are in code today. Items marked **recommended** are suggestions and are not implemented. Source IDs refer to rows in `permission-inventory.csv`.

## A. Requested restructure vs. current catalog

| Your module / screen | Current catalog | Gap | Type |
|---|---|---|---|
| TaskFlow Monitoring | No such module. Tasks live in module "Tasks". | Rename or regroup. | recommended |
| Task Management (was Tasks) | Screens "Task list", "Task details". Nav label already says Task Management. | Catalog still says "Tasks". | recommended |
| Employee Workload | Only `workload.view`, filed under "Reports and workload". | Move under Monitoring. No finer keys for its filters or columns. | recommended |
| Board | `tasks.status.change` (screen "Task board") only. | No view permission. `/board` route, `GET /tasks/board` and the nav item are not gated by a Board key. | recommended |
| TaskFlow Management | Three separate modules: Employees, Projects, Departments. | Regroup. | recommended |
| Communication Management | Two modules: Mail and Notifications. The nav already groups them. | Regroup. | recommended |
| Report | Module "Reports and workload", screen "Reports". | Split off workload. | recommended |
| Settings | Settings holds reminder, application and TFA (`settings.ai.*`). Role configuration (`roles.manage`) is its own module. | Regroup. | recommended |
| My Profile | `profile.*` (4 keys), all always-on and not assignable. | None, by design. | - |
| Not in your list | Dashboard (5 keys), AI assistant (2 keys), legal pages, login, account recovery, security setup. | Decide where Dashboard and the assistant belong. | open |

## B. Security and correctness findings

Ordered by severity.

1. **`roles.manage` can be given to a custom role (existing).**
   - Catalog: `roles.manage` has the default `IsAssignable = true`, and its description invites it.
   - Code: `RoleService` blocks only system roles and keys with `IsAssignable = false`.
   - The docs, the `RolesController` summary and the UI comment all say "Administrator only", so docs and code disagree.
   - Combined with item 2, a holder can create a role with `employees.role.assign`, then give anyone the Administrator role.
2. **No check on the target role when assigning roles (existing).** `UsersController.Create` and `Update` check only `employees.role.assign`. Nothing stops the target role being Administrator.
3. **`UsersController.Update` accepts a `Password` field (existing).**
   - Anyone with `employees.edit` can set it.
   - It needs no authenticator code and no `profile.password.change`.
   - No UI uses it. This is an API-only path that bypasses the MFA-gated password flow.
4. **Task delete has no per-task access check (existing).** `DELETE /tasks/{id}` needs `tasks.delete` but never checks that the caller can see the task.
5. **Over-broad reads.**
   - `GET /projects/{id}/tasks` needs only `projects.view` and ignores task scope (`tasks.view.all`).
   - `GET /reports` and `/reports/export` are org-wide for any `reports.view` holder.
   - `GET /mail/contacts` returns the org-wide directory to anyone with `mail.compose`.
   - `GET /projects/{id}/employees` returns the raw Domain `User` entity. No UI calls it.
6. **`TaskAccessHandler` lacks the manager team scope (existing).** `TaskService.CanAccessTaskAsync` adds it. Update, Reassign and SetRecipients rely on the service check.
7. **Endpoints with `[Authorize]` only**, which cannot be turned off for a role:
   - `GET /tasks`, `/tasks/board`, `/tasks/{id}`, `/tasks/assignees`, `/tasks/{id}/recipients` and `/tasks/{id}/comments`. These are scoped by data rules.
   - `GET /users/{id}`.
   - `GET /settings/application`. This is probably intended, because the header shows the application name.
   - `GET /dashboard/employee` and `/employee/analytics`. The analytics endpoint also stacks a redundant `[Authorize]` on its policy.
   - `/users/me*` and the MFA self-service endpoints. These are intended, as own-profile access.
8. **Orphan endpoints (no UI caller found):** `GET /dashboard/admin`, `/dashboard/manager`, `/dashboard/employee`, `PUT /tasks/{id}/recipients` and `GET /projects/{id}/employees`.
9. **`employees.role.assign` is enforced inline** by `UsersController.CanManageRoles()`, not by an attribute. It is easy to miss.

## C. UI controls with no matching backend or UI check

- **API enforced, no UI check.** Users without the key see buttons that return 403:

  | Key | UI controls |
  |---|---|
  | `mail.attachments` | Attach button; attachment download; attachment remove |
  | `notifications.manage` | Per-row "Mark read" button. Only "Mark all read" is gated. |
  | `assistant.actions.confirm` | Action card Confirm, Edit and Cancel |
  | `positions.create` | "Create new" in the position combobox |
  | `settings.ai.edit` | Model, server, API key and URL inputs. Only Save is hidden. |
  | `tasks.status.change` | Task details "Move to" buttons. The board is gated correctly. |

- **Screen not gated by the keys it needs:**
  - The admin, manager and employee dashboards show KPI cards and charts without `dashboard.analytics.view`. A role with `view.*` and no analytics key sees an error banner.
  - Dashboard period chips on the employee dashboard are not gated by `dashboard.filters.use`.
  - Employee details drawer "Edit" button is shown to everyone who opens it. The backend rejects the save without `employees.edit`.
  - Routes `/tasks/:id/activity` and `/tasks/:id/discussion` have no guard. A direct URL to activity gives a 403 error screen.
  - The project team dialog auto-opens after a project is created, even without `projects.members.manage`.

## D. Hidden cross-module dependencies

The catalog `Requires` field does not list these, so a role that lacks the other key gets 403s on dropdowns and dialogs.

| Screen or action | Also needs |
|---|---|
| Task create | `employees.view` (assignees), `projects.view`, `settings.reminders.manage` (reminder defaults) |
| Task reassign | `employees.view` |
| Task list, board and workload project filters | `projects.view` |
| Employee screen | `projects.view`, `departments.view` |
| Employee form positions | `projects.view` |
| Employee form role dropdown | `roles.manage` for `GET /roles` |
| Department members dialog | `employees.view` |
| Project team employee picker | `employees.view` |
| Reports project filter | `projects.view` |

Other mismatches:

- **Screen vs API keys differ.** `/employees` and `/departments` routes and menus need an action key (create, edit and so on), but the APIs need `employees.view` or `departments.view`. A role with only a view key sees no menu or route.
- **Employee form project allocations** need only `employees.create` or `employees.edit`, not `projects.members.manage`. Employee create also accepts `DepartmentIds` without `departments.members.manage`.
- **Workload is not narrowed to the manager's team.** It lists every active user whose role lacks `tasks.view.all`.
- **`tasks.update` description** mentions reminders, but the edit form covers only title, description, priority, dates and hours.
- **No Profile MFA card.** The endpoints exist, but setup is only in `/security-setup`, and Profile has no tabs.

## E. Hardcoded role checks (existing, candidates for centralization)

| Location | Check | Effect |
|---|---|---|
| `features/tasks/task-create.component.ts:115` | `user.role === 'Employee'` | Custom roles can never be assigned tasks. |
| `features/tasks/reassign.component.ts:69` | `getAll({ role: 'Employee' })` | Same. |
| `features/employees/employee-details-panel.component.ts:39` | `employee().role === 'Manager'` | Display. |
| `features/profile/profile.component.ts:81` | `user.role === 'Manager'` | Display. |
| `features/projects/project-team-dialog.component.ts:389-390` | `roleId === SYSTEM_ROLE_IDS.Manager`; mirrored in `ProjectService.cs:68` | Only the Manager system role can be project manager. |
| `features/departments/department-members-dialog.component.ts:198, 202` | `role: 'Manager'` / `'Employee'`; `DepartmentService.cs:166` requires `ManagerId` | Custom roles are never selectable. |
| `core/auth/role-routes.ts:4-5` | `Administrator` → `/admin`, `Manager` → `/manager` | Runs ahead of permission-based routing. |
| `features/employees/employee-list.component.ts:50` | Hardcoded role filter list | Custom roles missing from the filter. |
| `features/employees/employee-form-panel.component.ts:208-214` | System-role fallback with fixed ids | Used when `GET /roles` fails. |
| `features/legal/legal-page.component.ts:74` | `dashboardRouteFor(user.role)` without permissions | Custom roles land on `/me`. |
| `UserService.cs:518-527` | Last-administrator protection by `RoleId` | Intended safeguard. |

`manager-dashboard.component.ts:50` `isAdministrator()` is a permission check, not a role check.

## F. Duplicate or inconsistent definitions

- `employee-list.component.ts:154` accepts either `roles.manage` or `employees.role.assign` for the role filter, so two keys gate one control.
- Stale comments:
  - `tfa-settings.component.ts` says `settings.ai.manage`. That key is legacy, and the screen uses `settings.ai.view`.
  - `SettingsController` says "Administrator and Manager only". It requires `settings.reminders.manage`.
  - The employee form hint says "Only an Administrator can change a role", but `employees.role.assign` controls it.
  - `permissions.ts` says mail, notifications and the assistant "need no key". They are real, switchable keys seeded to all roles.
- Seven catalog keys have no frontend reference. `employees.view` and `departments.view` are expected, since the routes use action keys:
  `dashboard.analytics.view`, `tasks.recipients.manage`, `positions.create`, `departments.view`, `employees.view`, `mail.attachments`, `assistant.actions.confirm`.
- Every catalog key has a backend reference. No key is declared but unused on the backend.

## G. Controllable individually today vs. needs implementation

- **Already individually controllable (existing key):** 256 of the 412 recorded UI and API features, covering tasks (create, edit, delete, status, reassign, recipients, comment, activity), projects, departments, employees, reports, mail, notifications, assistant, settings, role configuration and dashboard analytics and filters.
- **Not individually controllable (156 features):** Board view, task list view, task details view, discussion view, workload filters and columns, individual dashboard KPI cards, charts and tables, `/users/{id}`, application settings read and employee dashboard routes.
- **Cannot be turned off by design:** the `profile.*` keys, own dashboard and own tasks.

## H. Possible breaking changes if implemented (not done)

- **Adding view keys for Board or task list.** This needs seeding so Manager and Employee keep access (`GrantToAllExistingRoles`, as mail did).
- **Renaming modules or screens.** Display strings only, but `permission-tree` and the matrix view read them (not checked for other readers). Keys must not change.
- **Making `roles.manage` non-assignable.** Any custom role that already holds it would lose access.
- **Adding `Requires` entries.** These auto-tick dependent keys in the Role Configuration tree. Existing roles are unaffected, but saving a role would add them.
- **Gating dashboard cards and charts.** This changes the screen for roles that currently see an error banner.
