# Design

## Context

Authorization today: `PermissionCatalog` (19 `PermissionDefinition` records with a free-text `Group`), tables `Roles`, `Permissions`, `RolePermissions`, `[RequirePermission(key)]` on controllers, permission claims refreshed from the database per request, `permissionGuard` on routes, `buildNavigation()` for the menu, and a flat checkbox screen (`permissions-management.component`). `AccessControlSeeder` inserts missing permissions on every start, grants Administrator everything, and grants Manager/Employee defaults only when a permission is first created. See proposal.md for motivation and the endpoint inventory it was built from.

Observed constraints: `tasks.view.all` already acts as a row-scope switch; the employee dashboard, task read/comment/status, mail, notifications, assistant chat, profile and MFA setup are open to any signed-in user; task delete and user delete are guarded by `roles.manage`; `/departments` and `/employees` routes demand `.manage` though their API needs `.view`.

## Goals / Non-Goals

**Goals:**
- One catalogue with a navigable hierarchy, authored once in code and seeded to the database.
- Fine-grained keys with no loss or widening of any role's current effective access.
- Backend enforcement per action; frontend hiding derived from the same keys.
- A Role Configuration screen that stays usable at about 120 permissions.

**Non-Goals:**
- Per-user overrides, permission audit log, row-level rules beyond the existing own/team/all scopes.
- Notification preferences, task progress percentage and Approve actions (not in the app).
- Changing authentication, MFA or session handling.

## Decisions

**1. Hierarchy lives in the code catalogue, not in the database.** (Revised during apply: the database has triggers that refuse any UPDATE or DELETE on `Permissions`, so metadata columns could not be filled for existing rows.) `PermissionDefinition` carries Module, Screen, Section, Action, Requires, ScopeGroup and ScopeRank; `GET permissions` merges it with the stored ids. The tree is built client-side from the flat list. Keys stay dotted and stable (`tasks.delete`, `dashboard.view.team`). No schema migration is needed.

**2. Old broad keys keep granting their children; new keys are granted once at creation.** (Revised during apply.) Old keys that were split (`dashboard.admin`, `dashboard.manager`, `projects.manage`, `departments.manage`, `employees.manage`, `settings.ai.manage`) are no longer listed or assignable, but rows already held stay and `EffectivePermissions.Expand` turns them into the finer keys when claims are built, so no role loses or gains access. When a new permission row is first created the seeder also grants it to roles that held a related key (`GrantFrom`), or to every existing role when the feature used to be open to all signed-in users (`GrantToAllExistingRoles`), and never again, so a later removal is not undone. Saving a role in the new screen drops its old keys and stores the finer ones explicitly. Employee deletion, task deletion and role assignment were never covered by `employees.manage`, so it does not imply them. Always-on features (own profile, mail, notifications, assistant chat, own dashboard, own tasks) are catalogue entries marked `AlwaysGranted`: shown locked in the tree, not stored, not in claims.

**3. Scope as ordered keys, not a value column.** Scope levels are modeled as separate ordered keys in a scope group (`dashboard.view.own` < `.team` < `.organization`; `tasks.view.own` < `tasks.view.all`). The UI renders them as one selector; the backend treats a higher key as implying lower ones. This reuses `RolePermissions` unchanged, keeps claims a flat string list, and maps exactly to today's `dashboard.manager`, `dashboard.admin` and `tasks.view.all`. Alternative: a `Scope` column on `RolePermissions` - needs claim and DTO redesign.

**4. Dependencies are declared in the catalogue.** A child action implies its screen's View (for example Delete tasks requires View tasks). The API rejects a save that violates this; the UI auto-enables the View when a child is checked and warns when a View is unchecked with children enabled.

**5. Self-service features are catalogued but locked on.** Own profile, mail, notifications, assistant chat and MFA setup are listed in the tree with `IsAssignable = false` and granted to every role, so the tree is complete and honest while behaviour matches today. Making them switchable later is a seed change, not a redesign.

**6. Fix route/API drift and mis-keyed deletes.** `tasks.delete` and `employees.delete` are new children of `tasks.update`/`employees.manage` respectively; the migration grants them to the roles that effectively could delete before (Administrator only, since `roles.manage` is Administrator-only). Route data uses the same View key the API uses.

**7. Enforcement stays single-sourced.** `Permissions.ts` constants are generated from, or checked against, `PermissionCatalog` by a test, so a key cannot exist on one side only. A catalogue-integrity test also asserts every `[RequirePermission]` key and every route/nav key is in the catalogue.

**8. Screen design.** Master/detail: role rail, module navigator with counts, tree panel with tri-state checkboxes, scope selectors, search and Enabled/Disabled filter, expand/collapse, copy-from-role, Duplicate-as-custom for system roles, optional matrix view (roles as columns), sticky save bar. Narrow screens use drill-in. Built with the existing `tf-*` design tokens; no new UI library.

## Risks / Trade-offs

- [Expansion migration mis-grants] -> Snapshot test comparing effective permissions per role before and after, using a seeded copy of current data; rollback by restoring the pre-migration database backup.
- [~120 endpoints/controls to gate; one missed gets exposed] -> Integrity test enumerating controller actions for an authorization attribute, with an explicit allow-list for open endpoints.
- [Scope keys confuse administrators] -> Single selector in the UI with plain-language labels; keys are never shown.
- [Hiding widgets without backend gating is cosmetic] -> Each widget's data comes from an endpoint or response field that is itself gated; widget keys only hide what the role may fetch.
- [Large screen scope] -> Tasks are ordered so catalogue, migration and enforcement land and ship before the UI redesign.

## Migration Plan

1. Add metadata columns and new permission rows (additive, nullable defaults).
2. Run the one-time expansion keyed on a stored catalogue version.
3. Re-point controllers and guards to child keys; old parent keys keep granting.
4. Deploy the new screen. Rollback: the additive columns and extra rows are ignored by the previous build, which only reads the old keys that remain granted.

## Open Questions

- Whether administrators later want Mail, Notifications and Profile to be switchable per role (currently locked on; deferrable seed change).
- Whether Dashboard widgets need to be individually hidden for Own scope or only gated by the key (assumed individually gated).
