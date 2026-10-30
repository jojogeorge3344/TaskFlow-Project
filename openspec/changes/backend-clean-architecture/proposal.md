# Proposal

## Why

REQ-04 calls for Clean Architecture with the Repository Pattern (Controller → Service → Repository → Database), and REQ-05 calls for a layer-first, module-based folder structure. The backend is split into four projects (Api / Application / Domain / Infrastructure) but does not follow those rules inside them:

- Services live in Infrastructure and inject the concrete `AppDbContext`. There are no repositories.
- `PasswordRecoveryController`, `TaskAccessHandler` and `SessionValidation` query the database directly, skipping layers.
- Controllers repeat cross-cutting work: 57 `catch` blocks and about 60 hand-written `IValidator<T>` calls.
- Error responses come in four different body shapes. Unexpected framework `InvalidOperationException` messages are returned to clients as 400 responses.
- Folders are grouped by feature, not by layer, and the module names don't match the module tree users see in Role Configuration.

## What Changes

- Merge `TaskFlow.Api`, `TaskFlow.Application`, `TaskFlow.Infrastructure` and `TaskFlow.Domain` into a **single `TaskFlow.Api` project**, laid out by layer: `Controllers/`, `Services/` (+ `Interfaces/`), `Repositories/` (+ `Interfaces/`), `Models/`, `Dtos/`, `Middleware/`, `Config/`, `Common/`, `Persistence/`, `Integrations/`.
- In every layer, organise code by the **module → sub-module hierarchy of `PermissionCatalog`**: Dashboard, Monitoring, Management, Communication, Reports, Settings, Profile, Assistant, plus `Auth` for pre-login flows. A catalog *section* gets a third-level folder only when it has its own controller or service.
- Introduce **per-module repositories** behind interfaces, built on a shared `BaseRepository<T>`, plus an `IUnitOfWork` that owns commits and transactions. Services depend only on repository interfaces. No `IQueryable` crosses the repository boundary.
- Move all business rules into services, including the dashboard visibility scopes. Repositories only run queries described by filter objects.
- Separate request/response DTOs from entities under `Dtos/<Module>/<SubModule>/`.
- Centralise cross-cutting concerns: an exception-handling middleware driven by a typed `AppException` hierarchy, an MVC validation filter that runs FluentValidation for every bound request, and a request-logging middleware. Remove the per-action try/catch blocks and hand-written validator calls.
- **BREAKING (API error body):** every error response becomes a single ProblemDetails shape (`status`, `title`, `detail`, optional `errors`, `traceId`), replacing `{ error }` / `{ message }` bodies. The frontend's shared `apiErrorMessage` already reads `detail`. The one component that reads `error.error` directly, and the integration tests that assert `error` or `message`, are updated in this change.
- **Behaviour change:** unexpected exceptions return 500 with a generic message instead of leaking internal messages as 400.
- **Security fix:** `GET /api/v1/projects/{id}/employees` returned the `User` entity, including the password hash, security stamp and protected MFA secret, to anyone who may view projects. It now returns account details only (the frontend does not call it).
- Add **architecture tests** that fail the build when a layer skips a level or depends on a concrete lower-layer type.
- Rename `UsersController` to `EmployeesController` to match the Employee Management module. All HTTP routes stay the same.
- Remove the template leftovers `WeatherForecastController` and `WeatherForecast`.

**Prerequisite:** close or archive the in-progress OpenSpec changes before the file moves begin, so they don't conflict with this restructure.

## Capabilities

### New Capabilities
- `backend-architecture`: the layering, dependency-direction, dependency-injection, DTO-separation and folder-structure rules the backend must satisfy (REQ-04, REQ-05 backend), verified by automated architecture tests.
- `api-error-handling`: the uniform error response contract, the status-code mapping for domain errors, validation-failure responses, and the guarantee that internal exception details are never returned to clients.

### Modified Capabilities
<!-- None: no main specs exist yet. -->

## Impact

- **Code:** every backend source file moves, and namespaces change. About 20 services (~5,300 lines) and the 9 analytics classes are rewritten to use repositories. 18 controllers lose their try/catch and manual validation.
- **Build/deploy:** `TaskFlow.sln` drops three projects. The Dockerfile already publishes only `TaskFlow.Api.csproj`, so it is unchanged. The Render deployment is unaffected.
- **Database:** no schema change. The 21 EF migrations (43 files) move into `Persistence/Migrations/`. Migration IDs in `__EFMigrationsHistory` are unchanged, so existing databases keep working.
- **Tests:** `TaskFlow.UnitTests` is repointed at the single project, and query-shape tests move to the repositories. 14 integration-test assertions on `error`/`message` change to `detail`. A new architecture test suite is added.
- **Frontend:** `login.component.ts` switches to `apiErrorMessage`. No route or payload changes otherwise.
- **Dependencies:** possibly `NetArchTest.Rules` (test-only) for the architecture tests.
