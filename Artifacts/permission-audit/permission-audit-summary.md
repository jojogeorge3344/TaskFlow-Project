# TaskFlow permission audit summary

Status: **draft for your validation. Nothing has been implemented or changed.** Branch `Develop_env`, 2026-10-24.
Method: static code read only. The API was not run and live database rows were not inspected, so role access shows catalog seed defaults.

## Update after the permission-module-restructure change

`permission-inventory.csv` (permission rows), `permission-inventory.json` and `role-permission-matrix.csv` were regenerated from the catalogue after that change: 60 catalogue keys (54 stored, 6 always on) in the new module grouping, including the new keys `board.view`, `board.filters.use`, `workload.filters.use`, `workload.tasks.open` and the Administrator-only `roles.administrator.assign`. `roles.manage` stays assignable, but only the Administrator can give it, Assign roles or the new key to a role.

The feature rows (412) and `dashboard-feature-inventory.csv` are still the snapshot taken before the change. Their module names were mapped to the new grouping, but they do not yet show the Board and Workload controls as individually controllable, and `permission-gaps.md` still describes the state before the change.

## Files in this folder

| File | Content |
|---|---|
| `permission-inventory.csv` | 473 rows. `record_kind` is `permission` (55 catalog keys), `permission (legacy)` (6) or `feature` (412 UI and API features). |
| `permission-inventory.json` | Same data as a Module → Submodule → Screen tree. Each screen lists its `permissions` and `features`. |
| `role-permission-matrix.csv` | 55 keys × Administrator, Manager, Employee and Custom role. |
| `dashboard-feature-inventory.csv` | 80 features on the admin, manager and employee dashboards. |
| `permission-gaps.md` | Gaps, API security issues, hardcoded checks and inconsistencies. |

## Roles inspected

| Role | Source | Notes |
|---|---|---|
| Administrator (id 1) | `SystemRoles`, `AccessControlSeeder` | Re-granted every assignable key on every start. |
| Manager (id 2) | Same | Seed defaults from `GrantedToManager`. Read-only in the UI. |
| Employee (id 3) | Same | Seed defaults from `GrantedToEmployee`. Read-only in the UI. |
| Custom roles | `RoleService`, Role Configuration | Hold whatever an administrator ticks. |

There is no "Team Lead" role in code. Manager is the team-level role, through `dashboard.view.team` and the team scope in `TaskService`.

## Hierarchy found (proposed grouping of your six modules)

Counts are (catalog permissions / recorded features).

```
TaskFlow Monitoring
  Task Management        9 / 82   (task list, create, details, discussion, activity, reassign)
  Board                  1 / 13   (only tasks.status.change; no view key)
  Employee Workload      1 / 11   (workload.view only)
TaskFlow Management
  Employee Management    7 / 30
  Project Management     6 / 31
  Department Management  5 / 16
Communication Management
  Mail                   4 / 23
  Notification           2 / 10
Report
  Reports                2 / 11
Settings
  Reminder Settings      1 / 6
  Application Settings   1 / 4
  TFA Settings           4 / 10
  Role Configuration     1 / 25
My Profile               4 / 13   (all always on)
Not in your list
  Dashboard (admin, manager, employee)  5 / 80
  TFA assistant                         2 / 9   (chat and action keys; floating panel)
  Login, account recovery, security setup, legal pages: 23 features, no role keys
  Shell (header, side menu, footer, routing): 14 features
```

`Task Management` counts the features of the task backend rows too. Workload has 10 UI rows plus one backend row.

## Counts

- 55 catalog permissions: 49 stored, 6 always-on (`dashboard.view.own`, `tasks.view.own`, `profile.*` ×4 — assigned to every user, not stored). Plus 6 legacy broad keys that expand to finer ones.
- 412 features recorded: 156 have no individual permission, and 256 are covered by an existing key.
- 80 dashboard features.
- 16 API controllers (excluding the sample WeatherForecast controller): every endpoint was listed. Frontend: 77 feature files, 183 frontend files scanned and 259 backend `.cs` files scanned for key usage (tests, migrations and build output excluded).
- Seven catalog keys have no frontend reference; see `permission-gaps.md` section F.

## Decisions I need from you

1. **Where does Dashboard go?** It isn't in your six modules. Options: a seventh module, or under TaskFlow Monitoring.
2. **Where does the TFA assistant go?** Chat and action keys are not in your list. Options: Communication Management, or its own module.
3. **Board permissions.** Should there be a separate Board view key, distinct from Task Management view? That would be a new permission, seeded so current roles keep access.
4. **Should `roles.manage` become Administrator-only?** Today any custom role can hold it, and the docs say otherwise.
5. **Which "NONE" features should get their own key?** The 156 rows are listed in `permission-inventory.csv` (`existing_key_status` = "NONE"). Filter on that column.

## Unresolved or unverified

- Live database role grants were not read. Manager and Employee columns assume seed defaults. System roles are read-only in the UI, so drift is unlikely but unconfirmed.
- No runtime test was done of any 403 or hidden-UI claim. The findings come from reading code. Where the two agents' notes said "partially verified" (about 60 task and dashboard rows, plus the rows marked likewise in management and settings), the row's `discovery_status` says so.
- Five task and dashboard rows have status "not found" in the source CSV.
- Permission tree rows from Role Configuration were captured as UI features, not as permissions.
- The `roles_with_access` column on feature rows is derived from key names found in each row's text, so rows that mention several keys show the union of their defaults.
- The 156 "NONE" rows include presentational items (labels, refresh buttons) that likely need no permission. They are not separated out.
- The 33 OpenSpec changes were not cross-checked against these findings. `module-based-role-permissions` (26/30 tasks) overlaps most.

## Next step

Review these files and tell me what to change. After you approve, I can propose an OpenSpec change (`/opsx:propose`) for the restructure, either new or folded into `module-based-role-permissions`. I will not implement anything before that.
