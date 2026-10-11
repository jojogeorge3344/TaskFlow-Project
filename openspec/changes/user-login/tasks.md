## 1. Preconditions

- [ ] 1.1 Confirm `bootstrap-solution-scaffold` is applied; verify `dotnet build` succeeds at the solution root and F5 shows the sample SPA populated by the API.
- [x] 1.2 Add `Jwt` settings (issuer, `AccessTokenMinutes=30`, `RefreshTokenDays=7`), the SQLite connection string, and the allowed SPA origin to `appsettings.json`, and put the signing key in user-secrets; verify `dotnet user-secrets list` shows the key and no secret appears in `appsettings.json`.

## 2. Data & domain foundation

- [x] 2.1 Add the `Role` enum and `User` entity (`Id`, `EmployeeCode?`, `FullName`, `Username`, `Email`, `PasswordHash`, `Role`, `IsActive`, `CreatedAt`, `UpdatedAt`, `LastLoginAt?`, `IsDeleted` — `DepartmentId` deferred per design D9) in `TaskFlow.Domain`; verify the project builds.
- [x] 2.2 Add `AppDbContext` in `TaskFlow.Infrastructure` with the `User` and `RefreshToken` sets, unique indexes on `Email` and `Username`, and the `IsDeleted == false` global query filter; verify the project builds.
- [x] 2.3 Create the initial EF Core migration for `users` and `refresh_tokens`; verify `dotnet ef database update` creates `taskflow.db` containing both tables and the unique indexes.

## 3. Auth infrastructure

- [x] 3.1 Define `ITokenService` in `TaskFlow.Application` and implement `JwtTokenService` in `TaskFlow.Infrastructure` to issue signed access tokens (claims: subject, role, name) and to mint/store hashed refresh tokens; verify unit tests cover issue/validate and rotation.
- [x] 3.2 Wire JWT bearer authentication, `PasswordHasher<User>`, and the `AdminOnly` / `ManagerOrAdmin` policies in `Program.cs`; verify a protected endpoint returns 401 without a token and 200 with a valid one.
- [x] 3.3 Add global exception handling returning RFC-7807 `ProblemDetails` and CORS for the configured SPA origin; verify an unhandled error returns a `ProblemDetails` body and a preflight from the SPA origin is allowed.
- [x] 3.4 Configure the refresh-token cookie (HttpOnly, Secure, SameSite) scoped to the auth path; verify login sets the cookie with those attributes and it is absent from JavaScript-readable storage.

## 4. Backend login endpoints

- [x] 4.1 Implement `AuthService.LoginAsync` (load by email or username, reject `IsActive=false`/`IsDeleted`, verify the hash, update `LastLoginAt`, issue tokens) with a FluentValidation `LoginRequestValidator`; verify unit tests cover email, username, wrong password, unknown identifier, and inactive.
- [x] 4.2 Implement `AuthService.RefreshAsync` (validate + rotate; reject revoked/expired) and `LogoutAsync` (revoke); verify unit tests cover rotation and post-logout rejection.
- [x] 4.3 Implement `AuthService.GetCurrentAsync` and add the thin `AuthController` with `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout`, and `GET /api/v1/auth/me`, setting the refresh cookie and returning user + role + access token; verify integration tests (`WebApplicationFactory`) cover 200, generic 401, inactive rejection, and refresh rotation.
- [x] 4.4 Assert in an integration test that no response body contains a password or password hash.

## 5. Seed data

- [x] 5.1 Add an idempotent startup seeder creating an Administrator, a Manager, and an Employee with hashed passwords (dev-only, configurable); verify a clean database yields exactly three accounts and a second run adds none.

## 6. Frontend core

- [x] 6.1 Convert the Angular shell to standalone bootstrap (`provideRouter`, standalone `AppComponent`), resolving bootstrap D6; verify `npm run build` succeeds and the app still serves.
- [x] 6.2 Add auth models, an in-memory token store, and a typed `AuthService` (login, startup refresh, logout, current-user signal); verify unit tests with `HttpTestingController` cover each call.
- [x] 6.3 Add `AuthInterceptor` (attach `Bearer`; on 401 attempt refresh then redirect to login) and `AuthGuard` / `RoleGuard`; verify unit tests cover token attachment, refresh-on-401, and guard blocking.
- [x] 6.4 Add the centralized role → route mapping (Administrator→`/admin`, Manager→`/manager`, Employee→`/me`); verify a unit test covers all three roles.

## 7. Login screen & routing

- [x] 7.1 Build the standalone `LoginComponent` (logo + name, identifier field, password field with show/hide, submit button, error area) with a reactive form; verify component tests cover show/hide, empty-input block, and the generic error on failure.
- [x] 7.2 Add the lazy public `/login` route and placeholder `/admin`, `/manager`, `/me` routes rendering a minimal role-aware welcome shell behind `RoleGuard`; verify post-login navigation reaches the correct route and unauthenticated access is blocked.

## 8. End-to-end verification

- [x] 8.1 Run `dotnet test` and `ng test`; verify all suites pass.
- [ ] 8.2 From a clean database with the seeded accounts, F5 and log in as each of the three roles; verify each lands on its dashboard and that a browser reload restores the session without re-entry.
- [x] 8.3 Verify against the spec: invalid credentials show the generic error, an inactive account is refused without disclosure, and a refresh after logout is rejected.
