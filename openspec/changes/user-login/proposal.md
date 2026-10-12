## Why

TaskFlow has approved requirements, a technical design, and (after `bootstrap-solution-scaffold`) a runnable but empty solution. Screen 1 — **User Login** (`Requirements/screens wise feature/01-login-screen.md`, TDD §9.1) — is the first end-to-end slice and the app-wide authentication skeleton that every later feature depends on. Building it now establishes the browser → API → SQLite thread and locks the session, RBAC, error, and routing patterns that all remaining screens reuse.

This change **assumes `bootstrap-solution-scaffold` is applied** (solution, four backend layers, Angular 19 workspace, SpaProxy). It delivers the Screen 1 vertical slice; the three role dashboards it redirects to are out of scope (see below).

## What Changes

- **Authentication foundation:** `User` domain entity + `Role` enum, `users` table via the first EF Core migration, PBKDF2 hashing through `PasswordHasher<User>`, JWT bearer configuration, RFC-7807 `ProblemDetails` error handling, and CORS for the SPA origin.
- **Seed credentials:** an Administrator account (no registration screen exists), so login is testable from a clean database; other roles are created from Employee Management.
- **Backend endpoints:** `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, `GET /api/v1/auth/me` (TDD §8.2).
- **Login rules (Screen 1 FR 1–7):** accept registered **email or username**, verify the password, **reject `IsActive=false` / `IsDeleted` accounts**, update `LastLoginAt`, return the user + role. Invalid/inactive accounts return a **single generic 401** (no user enumeration, §9.4).
- **Secure sessions (§9.2):** short-lived access JWT held **in Angular memory**; refresh token **hashed** in `refresh_tokens`, rotated on use, revocable on logout, delivered in an HttpOnly/Secure/SameSite cookie; session restored on reload via refresh.
- **Angular login feature:** `LoginComponent` (logo + name, email/username field, password field with show/hide toggle, login button, error message area), typed `AuthService`, `AuthInterceptor` (attach `Bearer`, handle 401), `AuthGuard` / `RoleGuard`, and lazy `auth` routes.
- **Role redirect (FR 3):** on success, navigate to the role's dashboard route — `/admin`, `/manager`, `/me`.

**Not in this change:** the role dashboards themselves (Screens 2/3/4) — `/admin`, `/manager`, `/me` are registered as placeholder routes that render a minimal role-aware "welcome" shell. Also out: password change (Screen 19), password reset, user management (Screen 5), any task/notification behaviour.

## Capabilities

### New Capabilities

- `user-auth`: authenticating a registered user by email or username, rejecting invalid or inactive accounts with a non-enumerating error, issuing/refreshing/revoking secure sessions, exposing the current user, and routing an authenticated user to the dashboard for their role.

### Modified Capabilities

_None_ — this change introduces the first spec; no existing capability's requirements change.

## Impact

- **Backend:** `TaskFlow.Domain` (`User`, `Role`); `TaskFlow.Application` (`AuthService` + interface, `LoginRequest`/`LoginResponse`/`UserDto`, FluentValidation validator); `TaskFlow.Infrastructure` (`AppDbContext`, `users` + `refresh_tokens` configuration, initial migration, seed); `TaskFlow.Api` (`AuthController`, JWT bearer + `PasswordHasher` wiring, `ProblemDetails` middleware, CORS, auth policies).
- **Frontend:** `core/` (`AuthService`, `AuthInterceptor`, `AuthGuard`, `RoleGuard`, auth models, token store); `features/auth/` (`LoginComponent` + template/styles, routes); placeholder dashboard routes in `app.routes.ts`.
- **Config/secrets:** `Jwt` settings (issuer, access lifetime, refresh lifetime), signing key via user-secrets/env var, SQLite connection string, allowed SPA origin.
- **New dependencies:** `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.EntityFrameworkCore.Sqlite` (+ `Design`), `FluentValidation` (all already named in the approved stack).
- **Prerequisite:** `bootstrap-solution-scaffold` must be applied first.
