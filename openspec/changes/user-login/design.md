## Context

See `proposal.md` — Why. Current state and constraints that shape the approach:

- Requires `bootstrap-solution-scaffold` to be applied first: `TaskFlow.sln`, four backend layers, Angular 19 workspace, SpaProxy, and test projects already exist. Requirements for *what* login must do are in `specs/user-auth/spec.md`; the authoritative technical reference is TDD §8.2, §9, §12.
- No `users` table, `DbContext`, migration, or seed exists yet — login cannot be exercised without them, and there is no registration screen, so credentials must be seeded.
- The bootstrap template produces an **NgModule**-style Angular app (bootstrap D6 deferred the standalone migration to "the first feature"). Login is that first feature and is where routing/guards land.
- The dev SPA talks to the API through the SpaProxy dev proxy under a single origin; the API runs behind HTTPS.
- Screen 1's expected result ("user sees the dashboard for their role") cannot be fully satisfied because Screens 2/3/4 are not built.

## Goals / Non-Goals

**Goals:**

- One end-to-end vertical slice: browser → API → SQLite → back, exercised by login.
- Establish the app-wide auth skeleton (token, interceptor, guards, error envelope) that later features reuse unchanged.
- Make login verifiable from a clean database via a seeded Administrator account (other roles are created from Employee Management; see change `app-shell-and-admin-ui`).

**Non-Goals:**

- Real role dashboards (Screens 2/3/4) — placeholder routes only.
- Password change (Screen 19), password reset, employee management (Screen 5), or any task/notification behaviour.
- Multi-factor auth, external identity providers, or account lockout/throttling beyond a single generic failure response.
- Production publish/hosting or CI.

## Decisions

**D1 — Include the auth foundation in this change; stub the dashboards.**
Rationale: `/auth/login` is meaningless without the `users` table, hashing, and JWT config, so those ship together as one reviewable slice. Dashboard components are excluded to keep the slice focused; `/admin`, `/manager`, `/me` are registered as placeholder routes rendering a minimal role-aware welcome shell.
_Alternative considered:_ a separate foundation change before login — more ceremony for a greenfield slice with no independent behaviour to ship.

**D2 — Specific token storage: access token in Angular memory, refresh token in an HttpOnly/Secure/SameSite cookie (TDD §9.2).**
The access token is never written to `localStorage`/`sessionStorage`. The refresh token lives only as a cookie scoped to the auth path, stored server-side as a hash, rotated on each refresh, revoked on logout. Because a memory-only access token is lost on reload, the SPA performs a **silent refresh on startup** to restore the session.
_Alternative considered:_ access token in `localStorage` — rejected (XSS exposure); refresh in body — rejected (available to JS).

**D3 — One generic authentication failure for every rejection reason.**
Unknown identifier, wrong password, inactive, and soft-deleted accounts all return the same 401 with a generic message, preventing user enumeration (§9.4). The distinction is recorded server-side (log) only.
_Alternative considered:_ distinct "account disabled" message — rejected as an enumeration/oracle leak; Screen 1 FR-4's "appropriate error" is satisfied by a clear generic message.

**D4 — Use integer auto-increment primary keys for `users` (and later tables).**
Resolves the TDD §7.2 `INTEGER / GUID` ambiguity now, because `users.Id` flows into the JWT subject, DTOs, and every future FK. Integer keys are simpler in SQLite and easier to read in demo data.
_Alternative considered:_ GUID keys — better for distributed generation, unnecessary for a single-instance SQLite app.

**D5 — Token issuance behind an `ITokenService` interface in Application, implemented in Infrastructure.**
Keeps JWT/signing concerns out of the Application layer and out of the thin `AuthController`, and lets unit tests substitute a fake issuer. `PasswordHasher<User>` is injected the same way.
_Alternative considered:_ issuing tokens directly in the controller — rejected (untestable, breaks layering in `agent.md` §4).

**D6 — Adopt standalone components for the login feature and convert the app bootstrap in this change.**
Resolves bootstrap D6 early: login is the first feature, its component and routing are written standalone, and the shell is switched to `provideRouter`/standalone bootstrap at the same time. Conversions for later features then follow an already-standalone baseline.
_Alternative considered:_ write login NgModule-style to minimise bootstrap risk — rejected, as it would leave the baseline divergent from the TDD §12 target and force a second migration later.

**D7 — Seed accounts via an idempotent startup seeder, not static SQL.**
Password hashes must be produced by `PasswordHasher` at runtime, so seeding is code-driven and idempotent (present accounts are skipped). Seeds only an Administrator; Manager and Employee accounts are created from Employee Management, which is how the other role redirects are verified. (The demo Manager and Employee seeds were removed in `app-shell-and-admin-ui`.)
_Alternative considered:_ a SQL script in `database/seed/` — can't produce PBKDF2 hashes cleanly, and would drift from the model.

**D8 — Cache the role → route mapping in one place.**
`Administrator → /admin`, `Manager → /manager`, `Employee → /me`, centralised on the client so both post-login navigation and any later "already authenticated" redirect use the same source.

**D9 — Defer `DepartmentId` and the `departments` table to the employee/department change.**
TDD §7.2 gives `users` a nullable `DepartmentId` FK → `departments`, but `departments` is not introduced until Screens 5/6. The `User` entity in this change omits `DepartmentId` so the initial migration does not drag an unrelated table into the login slice; the column and FK are added with that later change. Nothing in Screen 1 depends on a department.
_Alternative considered:_ create a minimal `departments` table now to keep the FK — rejected as scope creep for a login slice.

## Risks / Trade-offs

- **Memory-only access token + page reload logs the user out** → silent refresh on app startup; if refresh fails, fall back to the login screen.
- **Cookie/CORS behaviour across the dev proxy (SameSite, Secure)** → call the API via the relative `/api` base so the browser sees one origin; scope the refresh cookie path to the auth endpoints; verify in the HTTPS dev flow.
- **Standalone conversion of the template shell (D6) can break existing wiring** → do the conversion first and confirm the app still builds/serves before adding login UI.
- **Generic errors can slow legitimate support** → log the specific reason server-side while returning the generic message to the client.
- **Seeding real credentials in git** → seed only non-secret demo accounts with configurable/dev-only passwords; never commit production secrets, and keep the JWT signing key in user-secrets/env vars.
- **Rotating refresh tokens under concurrent tabs** → latest-token-wins; a stale token fails and the tab re-authenticates. Acceptable for a single-user demo scale.

## Migration Plan

Greenfield — no existing data to migrate.

1. Add the `users`/`refresh_tokens` model and the **initial** EF Core migration; apply with `dotnet ef database update`.
2. Run the idempotent seeder on startup to create the demo accounts.
3. Rollback: revert the change and recreate `taskflow.db` (no production data depends on it).

## Open Questions

- Whether logout revokes only the current device's refresh token or all of the user's tokens (multi-device sessions). The spec requires the presented token to be revoked on logout; a broader "revoke all" is a later refinement and does not change the specs or the task breakdown.
