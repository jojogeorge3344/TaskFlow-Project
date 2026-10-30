# Design

## Context

See proposal.md (Why) for motivation. Current state, measured in the code:

- **4 projects:** `TaskFlow.Domain` (31 entities, enums, `Rules/`, `Security/PermissionCatalog`), `TaskFlow.Application` (service interfaces, DTOs, validators, plus `AuthService`/`MfaService`/`PasswordRecovery`, which use `IApplicationDbContext`), `TaskFlow.Infrastructure` (all other service implementations, `Analytics/`, `Assistant/`, `Settings/`, `Persistence/` with `AppDbContext`, 21 migrations (43 files) and seeders), and `TaskFlow.Api` (18 controllers, auth handlers, middleware).
- **About 43 classes outside `Persistence/` touch the DbContext.** That is 20 in `Services/`, 8 in `Analytics/`, 4 in `Assistant/`, 3 in `Settings/`, 3 in Application, and 3 in Api (`PasswordRecoveryController`, `TaskAccessHandler`, `SessionValidation`). There are about 46 `SaveChangesAsync` calls, 5 explicit transactions or bulk updates, and about 230 `.Select` projections.
- **Errors:** 72 `throw new InvalidOperationException` and 11 `UnauthorizedAccessException`, plus about 10 custom exception types with no shared base. Controllers map them in 57 `catch` blocks into four body shapes: `{ error }` ×21, `{ message }` ×10, `Problem(detail)` ×11, and `ValidationProblem` ×27. The frontend's `shared/ui/api-error.ts` already reads `detail`, then `error`, then `message`, then `errors`.
- **Validation:** validators are registered with `AddValidatorsFrom…` but invoked by hand in 11 controllers.
- **Tests:** 95 unit-test files reference Application, Domain and Infrastructure directly. 48 integration-test files reference only Api via `WebApplicationFactory<Program>`.
- The Dockerfile already restores and publishes only `src/TaskFlow.Api/TaskFlow.Api.csproj`.

## Goals / Non-Goals

**Goals:**
- One project that satisfies every scenario in `specs/backend-architecture` and `specs/api-error-handling`, with the layer rules enforced by tests rather than by convention.
- No behaviour change beyond the two the proposal names (the unified error body and generic 500s).
- Each step leaves the solution building with all tests green, so the work can pause between modules.

**Non-Goals:**
- No new features, no schema changes, no route changes.
- No CQRS/MediatR, no AutoMapper, no generic specification framework. Mapping stays as hand-written static mappers.
- No performance tuning beyond keeping existing queries server-side.

## Decisions

### D1. Single project, layer-first, with test-enforced boundaries
All code merges into `TaskFlow.Api`. The project-reference boundary is replaced by architecture tests (NetArchTest.Rules, test-only) that encode each `backend-architecture` scenario.
- *Alternative considered:* keep the four projects and mirror the tree inside each. That would be stricter (the compiler enforces it), but it was rejected by the user in favour of matching the REQ-05 reference tree literally.

### D2. Target layout

```
TaskFlow.Api/
  Controllers/<Module>/<SubModule>[/<Section>]/   <Name>Controller.cs
  Services/Interfaces/<Module>/...                I<Name>Service.cs
  Services/<Module>/<SubModule>[/<Section>]/      <Name>Service.cs, pure business-rule classes
  Repositories/Interfaces/<Module>/...            I<Name>Repository.cs, IUnitOfWork.cs (root)
  Repositories/<Module>/<SubModule>[/<Section>]/  <Name>Repository.cs
  Integrations/Interfaces/ + Integrations/<Area>/ AI providers, JWT, TOTP, key protectors, file store
  Models/Entities/  Models/Enums/                 former TaskFlow.Domain
  Dtos/<Module>/<SubModule>/                      *Request, *Response/*Dto, Validators/
  Middleware/                                     exception handling, request logging, MFA gate, session validation
  Filters/                                        ValidationFilter
  Config/                                         DI extension methods per layer, options classes, auth policies
  Common/  Exceptions/ Security/ (PermissionCatalog, keys, policies) Constants/ Utils/ BaseRepository.cs
  Persistence/  AppDbContext, Configurations/, Migrations/, Seeders/
```

Module map (from `PermissionCatalog`):

| Module / Sub-module | Current code that moves there |
|---|---|
| `Auth` (+ `Mfa`, `PasswordRecovery`) | AuthController, MfaController (sign-in), PasswordRecoveryController, AuthService, AttemptLimiter |
| `Dashboard` (+ `Analytics`) | DashboardController/Service, Admin/Manager/Employee analytics and scopes |
| `Monitoring/TaskManagement` (+ `Discussion`, `Activity`) | TasksController, TaskService, TaskCommentService, TaskActivityService |
| `Monitoring/Board` | none on the backend (uses TaskManagement endpoints) |
| `Monitoring/EmployeeWorkload` | WorkloadController/Service |
| `Management/EmployeeManagement` (+ `Allocations`) | UsersController → `EmployeesController` (route `api/users` kept), UserService, UserAllocationService |
| `Management/ProjectManagement` (+ `Positions`) | ProjectsController/Service, PositionsController/Service |
| `Management/DepartmentManagement` | DepartmentsController/Service |
| `Communication/Mail` (+ `Attachments`) | MailController, MailService, MailAttachmentService |
| `Communication/Notification` (+ `Reminders`) | NotificationsController/Service, NotificationPublisher, ReminderService, ReminderSchedulerService |
| `Reports` | ReportController/Service, ReportCsv |
| `Settings/ApplicationSettings`, `ReminderSettings`, `RoleConfiguration`, `TfaSettings` | ApplicationSettingsController/Service, SettingsController's reminder endpoints, RolesController/RoleService, AI provider config endpoints and AiProviderConfigService |
| `Profile` | own-profile, password and own-MFA endpoints and service methods |
| `Assistant` | AssistantController, AssistantService, AssistantActionService, read/action tools |

Domain `Rules/` (TaskStatusTransitions, ReminderCalculator, …) are pure business rules. They go to `Services/<Module>/<SubModule>/Rules/` for the module that owns them. Rules shared across modules go to `Common/Rules/`.

*Alternative considered:* a flat `Services/` folder. Rejected because REQ-05 requires module sub-folders.

### D3. Repositories: base class plus intent-named methods (option C)
- `Common/BaseRepository<T>` provides `GetByIdAsync`, `AddAsync`, `Remove`, and `ExistsAsync` over `AppDbContext`.
- Each sub-module has `I<Name>Repository : IBaseRepository<T>`, adding intent-named queries that take filter records (e.g. `TaskListFilter { AssigneeIds, ProjectIds, Status, From, To, Page }`).
- Rules: no member returns `IQueryable`, and repositories never call `SaveChanges`.
- **Read models:** writes and detail reads return entities, which the service maps to DTOs. List and analytics reads may project straight into response DTOs inside the repository, so the ~230 projections stay in SQL.
- *Alternatives considered:* a generic `IRepository<T>` exposing `IQueryable` (leaks EF into services and fails REQ-04 in substance), and pure per-module repositories with no base (repeats CRUD about 20 times).

### D4. Unit of work
`IUnitOfWork` exposes `SaveChangesAsync()` and `ExecuteInTransactionAsync(Func<Task>)`, implemented over the scoped `AppDbContext`. Because every repository shares that scoped context, one `SaveChangesAsync` commits a whole use case. The 5 explicit transaction or bulk-update sites move behind repository methods (bulk) or `ExecuteInTransactionAsync` (multi-step).

### D5. Visibility scopes become service logic
`Admin/Manager/EmployeeDashboardScope` and `TaskAccessScope` currently compose `IQueryable` filters. They are split in two:
- A **scope service** works out the allowed ids (team members, managed projects, or own id) using repositories. This is the business rule.
- The repository receives those ids in the filter record and applies them in SQL.

`AnalyticsScopeTests`, `ManagerScopeTests` and `ManagerDrillDownScopeTests` cover the rule. `AdminAnalyticsQueryShapeTests` moves to the analytics repository tests.

### D6. Integrations are a sibling of repositories
External systems (Gemini/OpenAI/Ollama providers, `ChatModelGateway`, `JwtTokenService`, `LocalMailFileStore`, `AiKeyProtector`, `MfaSecretProtector`, TOTP) live in `Integrations/` behind interfaces in `Integrations/Interfaces/`. Services may call integrations, but controllers may not. The architecture tests encode this.

### D7. Request pipeline components use services
`TaskAccessHandler`, `SessionValidation` and `PasswordRecoveryController` currently read the DbContext. They will call `ITaskAccessService`, `ISessionService` and `IPasswordRecoveryService` instead.

**Seeders and `SettingsInitializer`** stay in `Persistence/Seeders/` and may use `AppDbContext` directly. Startup data seeding is part of persistence, not a request flow, and the architecture tests exempt `Persistence.*`.

### D8. Exception hierarchy and mapping

```
Common/Exceptions/AppException (abstract: StatusCode, Title)
  BusinessRuleException 400   <- the 72 InvalidOperationException throws
  RequestValidationException 400 (field errors)  <- ArgumentException uses in the assistant provider endpoints
  ForbiddenException 403      <- UnauthorizedAccessException, AnalyticsForbiddenException
  NotFoundException 404       <- KeyNotFoundException, AnalyticsEntityNotFoundException
  ConflictException 409       <- AssistantActionConflictException, the MailController 409 case, concurrency
  RateLimitedException 429    <- AssistantRateLimitedException
```

Existing custom exceptions (`MailValidationException`, `RoleException`, `AnalyticsFilterException`, `AssistantRequestException`) are re-parented under the matching subclass and keep their names and titles. `RoleException` currently maps to several statuses through `ToProblem`, so it carries its status code.

`Middleware/GlobalExceptionHandler`, registered as an `IExceptionHandler` next to the existing `UseExceptionHandler`, works like this:
- An `AppException` becomes a ProblemDetails with its status, title and message as `detail`, plus `traceId`. Validation exceptions add `errors`.
- `DbUpdateConcurrencyException` becomes 409. This absorbs the existing `ConcurrencyExceptionHandler`.
- Anything else becomes a 500 with the generic detail "Something went wrong. Please try again.", and the exception is logged at Error with the `traceId`.

The task "missing or deleted → 403" rule stays: the service throws `ForbiddenException` for both cases.

**Provider test endpoint:** `ProviderFailureException` and `ProviderNotConfiguredException` currently become a `200` result DTO. That is a result, not an error, so the service catches them and returns `AiProviderTestResultDto`. Behaviour is unchanged, and the controller stays free of catch blocks.

### D9. Validation filter
`Filters/ValidationFilter` is a global MVC async action filter. For each action argument it resolves `IValidator<T>` from DI. If a validator exists it runs it, and on failure it short-circuits with `ValidationProblem` (400, `errors`). Validators move to `Dtos/<Module>/<SubModule>/Validators/`. Hand-written `ValidateAsync` calls are deleted. The assistant tools that validate internally (`AssistantActionService`/`Tools`) keep calling validators, because they are not HTTP-bound.

### D10. Request logging
`Middleware/RequestLoggingMiddleware` writes one `ILogger` entry per request with method, path, status, elapsed ms, user id claim and `traceId`. It logs no bodies, because request bodies carry passwords, MFA codes and mail content. It runs after `UseForwardedHeaders` and before authentication, so 401s are logged too.

### D11. DI registration per layer
`Config/` holds `AddRepositories()`, `AddServices()`, `AddIntegrations()` and `AddCrossCutting()`, each listing its registrations explicitly by module. `Program.cs` calls these instead of inline registrations. *Alternative considered:* assembly scanning (Scrutor). Rejected because explicit lists are easier to read and grep for a project of this size, and the DI-resolution test (spec: "Every interface has a registration") catches omissions.

### D12. Namespaces
Namespaces follow the folders: `TaskFlow.Api.Services.Monitoring.TaskManagement`, and so on. EF migration designer files keep working because they reference `AppDbContext` by type. Their namespace moves to `TaskFlow.Api.Persistence.Migrations`, and migration IDs are unchanged.

## Risks / Trade-offs

- [Huge diff, hard to review] → Work in the order of `tasks.md`: a mechanical merge first, then one module per commit. Each step builds with tests green.
- [Collision with ~20 in-progress OpenSpec changes] → Finish or archive them before step 1 (prerequisite in the proposal).
- [A rewrite of the analytics queries changes results or makes them slower] → The analytics service and scope unit tests and the analytics endpoint integration tests must pass unchanged. Query-shape tests move with the code, so a client-side evaluation regression would fail them.
- [Losing the compiler-enforced boundary lets violations creep back] → The architecture tests run in the normal `dotnet test` and so fail CI.
- [Hidden callers parse `{ error }` / `{ message }`] → Grep showed one direct reader (`login.component.ts`) and 14 test assertions. All are updated in the same change. `apiErrorMessage` already prefers `detail`.
- [Generic 500 hides useful messages that used to reach users through `InvalidOperationException`] → Every intentional `InvalidOperationException` becomes a `BusinessRuleException` with the same message before the middleware is switched on. Only truly unexpected exceptions lose their text.
- [Migration designer files break on namespace change] → Change the namespaces with a single scripted edit, then run `dotnet ef migrations list` and the `*MigrationTests` unit tests to confirm.
- [Unit tests construct services with `AppDbContext` (SQLite in-memory harnesses)] → Harnesses build real repositories over the same in-memory context, so tests still exercise real SQL. Do not mock repositories.

## Migration Plan

1. Merge the projects, with no behaviour change, then deploy to the Render dev environment and smoke-test login, tasks and dashboard.
2. Cross-cutting (exceptions, middleware, filter), then deploy to dev. This step contains the user-visible error-body change.
3. Repositories and module moves, one module per commit. Deploy to dev after each module group.
4. Turn on the architecture tests for the whole assembly.

**Rollback:** every step is a normal commit on a feature branch with no database change, so a revert is enough. The database is untouched throughout.

## Open Questions

- Should NetArchTest.Rules be used, or a small reflection-based helper in the test project? Either satisfies the specs. Decide when writing the first architecture test.
