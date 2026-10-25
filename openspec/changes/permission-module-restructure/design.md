# Design

## Context

- The permission tree in Role Configuration is built from `PermissionCatalog.All` in code (`RoleService` maps each definition's `Module`, `Screen`, `Section`), not from database columns. Regrouping is therefore a code change, with no data migration for grouping.
- `AccessControlSeeder` runs on every start. It inserts permissions missing from the database and gives them to Manager, Employee and existing roles only at that moment, using the catalogue flags `GrantedToManager`, `GrantedToEmployee`, `GrantToAllExistingRoles` and `GrantFrom`. The Administrator receives every permission on every start. The seeder only inserts.
- `PermissionKeys.cs` (backend) and `permissions.ts` (frontend) list the same keys, and a backend test fails when they differ.
- `roles.manage` is declared with the default `IsAssignable = true`. The earlier `module-based-role-permissions` change describes it as Administrator-only and not assignable. See proposal.md for why this matters.
- Board currently has no key. `/board` has no route guard and `GET /tasks/board` is `[Authorize]` only. The board's own-versus-all task scope is applied in `TaskService`.
- Evidence for all of the above: `Artifacts/permission-audit/permission-gaps.md`.

## Goals / Non-Goals

**Goals:**
- Make the catalogue and Role Configuration match the requested module structure.
- Give Board and Employee Workload their own permissions without removing anyone's current access.
- Close the role-escalation paths.

**Non-Goals:**
- Controlling every uncontrolled feature (see proposal Out of scope).
- Changing task scope rules or dashboard behavior.
- Per-user permission overrides.

## Decisions

1. **Grouping is metadata in the catalogue; keys stay as they are.** Change each definition's module, screen and section strings, and add a submodule level only if the tree needs one. Alternative: rename keys to match the new modules. Rejected: it breaks stored grants, the frontend and tests, and the earlier design promised stable keys.
2. **Module and submodule shown as separate fields.** Today the tree is Module → Screen → Section. Submodule (for example Board under TaskFlow Monitoring) maps to the existing Screen level, so no new tree depth is added. Alternative: add a fourth level. Rejected: it changes the Role Configuration layout for little gain.
3. **New keys.** `board.view`, `board.filters.use`, `workload.filters.use`, `workload.tasks.open`. Names follow the existing `<area>.<feature>.<action>` pattern. All four are assignable and none is always-on.
4. **Seed new keys to current holders with the existing flags.**
   - Board keys: `GrantedToManager`, `GrantedToEmployee` and `GrantToAllExistingRoles`, so every role that could open the board still can.
   - Workload keys: `GrantFrom = workload.view`, so anyone who held workload view gets both.
   - Alternative: let administrators grant them manually. Rejected: it would remove the board from every role on upgrade.
5. **`board.view` guards the route, menu item and `GET /tasks/board`.** It does not change which tasks appear, because task scope still applies. `board.filters.use` only hides the filter controls. The API for the board takes no filter parameters today, so no server rule is needed for filters.
6. **Workload filters and open-tasks are UI-level permissions.** The audit shows the filters are client-side or use `projects.view` data, and the tasks link is a navigation target that opens the task list. The task list still enforces task scope, so no new server check is added. Alternative: server enforcement for each. Rejected: nothing server-side to enforce.
7. **`roles.manage` stays assignable; the privilege-raising keys do not.** A delegated role manager must be able to create and edit custom roles, so Role Configuration is assignable again. What is reserved for the Administrator is giving out the keys that raise privilege: `roles.manage`, `employees.role.assign` and the new `roles.administrator.assign`. `RolesController` refuses a create or update from a caller without `roles.administrator.assign` when the requested keys include any of the three, or when the role being edited already holds one (a save would silently drop it). Alternatives: (a) make `roles.manage` Administrator-only (rejected by the user, who wants delegation), (b) allow a holder to grant only keys they hold themselves (kept as a possible later tightening, not needed to close the escalation path).
8. **A new Administrator-only permission, `roles.administrator.assign`, decides who may hand out the Administrator role.** It is stored, not assignable, and the seeder gives it to the Administrator on every start. In `UsersController` create and update, choosing Administrator as the role, or moving anyone into or out of it, requires it in addition to `employees.role.assign`. The role list for the employee form lists Administrator only for callers who hold it. Alternative: check the caller's role name. Rejected: the audit lists hardcoded role checks as a problem. Reusing `roles.manage` for this no longer works now that a delegate can hold it.
9. **Employee update ignores `Password`.** The field stays on the request model so old clients do not break, and the service never applies it. Alternative: remove the field. Deferred: removing it is a contract change that can be done separately.
10. **Roles list for the employee form.** Add a read endpoint, or relax the existing one, that returns assignable roles to holders of `employees.role.assign` or `roles.manage`. The existing `GET /roles` (which returns permission details) keeps requiring `roles.manage`. Alternative: put the role list in the employee list response. Rejected: it mixes concerns.
11. **Activity route guard** uses the existing `tasks.activity.view` through `permissionGuard`, redirecting to the existing not-permitted handling.

## Risks / Trade-offs

- [A delegated role manager holds Role Configuration and an administrator also gave their role Assign roles] → They still cannot give out the Administrator role without `roles.administrator.assign`, and cannot grant either key to a role. Review which roles hold both before release.
- [A role seeded without `board.view` by mistake loses the board] → Test that Manager, Employee and a custom role created before the upgrade all hold the key after seeding.
- [Display renames confuse administrators used to "Tasks"] → Names change in one release; keys, descriptions and behavior stay.
- [New keys not mirrored in the frontend] → Update `permissions.ts` in the same task; the existing key-list test catches a mismatch.
- [`workload.tasks.open` hides only a link, so the task list URL is still reachable] → Acceptable: task scope still applies. State this in the permission description.
- [Seeder only runs inserts, so a seeding bug is hard to repair] → Cover seeding with a test against a database created before the change.

## Migration Plan

1. Deploy the catalogue and key changes. The seeder adds the four new keys and grants them to current holders at first start.
2. No data migration for roles: existing grants of `roles.manage` stay as they are.
3. Rollback: revert the release. The new permission rows stay unused.

## Open Questions

- Should a role manager be limited to granting only keys they hold themselves? Not needed to stop escalation to Administrator; can be added later.
- Should the discussion route (`/tasks/:id/discussion`) get its own view permission later? It has none today and is outside this change.
