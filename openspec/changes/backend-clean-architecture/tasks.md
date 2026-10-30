# Tasks

## 0. Prerequisite

- [x] 0.1 Confirm with `openspec list` that no in-progress change still plans edits to backend files (user decision: leave them open, since their remaining tasks are manual checks or frontend-only). Create branch `backend-clean-architecture` from up-to-date `Develop_env` (user decision)

## 1. Merge into a single project (no behaviour change)

- [x] 1.1 Move `TaskFlow.Domain` entities and enums into `TaskFlow.Api/Models/Entities` and `Models/Enums`. Move `Domain/Security` into `Common/Security`. Park `Domain/Rules` in `Common/Rules` for now (redistributed per module in groups 4–11). Update namespaces and confirm `dotnet build` succeeds
- [x] 1.2 Move `TaskFlow.Infrastructure/Persistence` to `Persistence/` (DbContext, `Seeders/`) and `Migrations/` to `Persistence/Migrations`, with a scripted namespace change. Verify `dotnet ef migrations list` shows all 21 migrations (43 files with designers and snapshot), and that the `*MigrationTests` unit tests pass
- [x] 1.3 Move the remaining Application and Infrastructure files into temporary `Legacy/Application/...` and `Legacy/Infrastructure/...` folders inside `TaskFlow.Api`. Merge the package references into `TaskFlow.Api.csproj`, and confirm `dotnet build` succeeds
- [x] 1.4 Remove the three projects from `TaskFlow.sln` and delete their folders. Repoint `TaskFlow.UnitTests.csproj` to `TaskFlow.Api` only. Verify the full `dotnet test` passes (unit and integration). Done: no new failures; the 5 failures present on `Develop_env` before this change (PermissionCatalog key count, 2 RolesEndpoints delegated-manager, 2 TasksEndpoints create) are unchanged and out of scope
- [x] 1.5 Delete `WeatherForecastController` and `WeatherForecast` (and repoint `AuthenticationWiringTests` and `TaskFlow.Api.http`). Verify the Dockerfile's `dotnet publish` command succeeds and the published `TaskFlow.Api.dll` starts on port 10000 against an empty storage folder and answers `POST /api/v1/auth/login` (401 for bad credentials; successful login is covered by `AuthEndpointsTests`). Docker is not installed on this machine, so the image itself was not built

## 2. Central cross-cutting concerns

- [x] 2.1 Add `Common/Exceptions` (`AppException` and the subclasses from design D8), and re-parent the existing custom exceptions. Verify with unit tests asserting each type's status code and title
- [x] 2.2 Replace intentional `InvalidOperationException`, `UnauthorizedAccessException`, `KeyNotFoundException` and `ArgumentException` throws in services with the matching `AppException` subtype, keeping the messages. Verify with a grep that no `throw new InvalidOperationException` remains outside `Persistence/` and `Integrations/`. Done: the two remaining throws (`AssistantCaller` missing id claim, `ChatModelProviderRegistry` unknown provider) are internal invariants left as `InvalidOperationException` on purpose so they surface as a generic 500 (design D8)
- [x] 2.3 Add `Middleware/GlobalExceptionHandler` (absorbing `ConcurrencyExceptionHandler`) to emit ProblemDetails with `traceId`, and generic 500s. Verify with integration tests covering 400/403/404/409/429 bodies and a 500 that does not echo the exception message (spec api-error-handling)
- [x] 2.4 Add the global `Filters/ValidationFilter` and delete every hand-written `ValidateAsync` call in controllers. Verify with an integration test that an invalid request returns 400 with field `errors` (done with an empty login, which needs no account), plus the existing validator tests. Note: six validators that no endpoint ran before (`CreateUserRequest`, `UpdateUserRequest`, `UserStatusRequest`, `CreateProjectRequest`, `UpdateProjectRequest`, nested `UpdateAiProviderRequest`) are now enforced; their rules match the frontend forms, and the stale password rule in `UpdateUserRequestValidator` was removed because an update ignores the password
- [x] 2.5 Delete every controller `try`/`catch` (57 blocks). Move the AI provider test-result handling into the service (design D8). Verify that all integration tests pass after updating the `error`/`message` body assertions to `detail` (5 helpers; the other `message` reads are success DTO fields). Also converted the remaining hand-built `{ error }` responses (429 throttles, wrong MFA code, read-only roles) and the MFA setup gate body to problem details, keeping the gate's `code`
- [x] 2.6 Add `Middleware/RequestLoggingMiddleware`. Verify with a `CapturingLoggerProvider` integration test that one entry has method, path, status, duration and user id, and no body
- [x] 2.7 Switch `frontend/src/app/features/auth/login/login.component.ts` to `apiErrorMessage`. Verify with a login spec that a refused authenticator code shows the server's `detail` message (22/22 login specs pass; a manual login was not run because no test credentials are available to this session)

## 3. Architecture test harness

- [x] 3.1 Add an `ArchitectureTests` class to `TaskFlow.UnitTests` (NetArchTest.Rules, chosen because it reads IL and so also sees dependencies used inside method bodies), with every rule from spec backend-architecture scoped to the namespaces migrated so far (`Controllers`, `Services`, `Repositories`, `Dtos`). Verify the suite runs and passes on the empty namespaces. Mutation check: with the pending list emptied, the suite flags exactly the known violations (TaskAccessHandler, PasswordRecoveryController, ProjectsController.GetEmployees returning an entity)
- [x] 3.2 Add `IUnitOfWork`, `Common/BaseRepository<T>` and `Config/` DI extension methods (`AddRepositories`, `AddServices`, `AddIntegrations`, `AddCrossCutting`), and move the existing registrations out of `Program.cs`. Verify with a DI-resolution test that resolves every registered service interface. Done: 29 interfaces resolve; a diff of the old Program.cs registrations against Config/ shows none lost

## 4. Module: Settings

- [x] 4.1 `Settings/ApplicationSettings` and `Settings/ReminderSettings`: create repositories, services, controllers and DTOs/validators in their module folders. Move `AppClock`'s settings read behind the repository. Verify that `ApplicationSettingsServiceTests`, `ReminderSettingsServiceTests`, `AppClockTests` and `SettingsEndpointsTests` pass
- [x] 4.2 `Settings/RoleConfiguration`: `RoleRepository`, `RoleService` and `RolesController` in module folders. Verify that `CustomRolePermissionTests`, `RolesEndpointsTests` and `ModuleBasedPermissionEndpointsTests` pass
- [x] 4.3 `Settings/TfaSettings`: AI provider config repository, service and endpoints. Move the providers to `Integrations/Ai`. Verify that `AiProviderConfigServiceTests`, `AssistantProviderEndpointsTests` and `ProviderFailureHandlingTests` pass. Done: the provider-config endpoints moved out of AssistantController into Controllers/Settings/TfaSettings/AiProvidersController (same routes); ChatModelGateway went to Services/Assistant (see design D6)

## 5. Module: Management

- [x] 5.1 `Management/DepartmentManagement`: repository, service, controller and DTOs. Verify that the `DepartmentsEndpointsTests` and department validator tests pass
- [x] 5.2 `Management/ProjectManagement` (+ `Positions`): repositories, services, controllers and DTOs. Verify that `ProjectTeamEndpointsTests` and `ProjectTaskTests` pass. Done: `GET /projects/{id}/employees` returned raw `User` entities (password hash, security stamp, protected MFA secret) to anyone allowed to view projects; it now returns `ProjectEmployeeDto` without credentials, covered by `TheProjectEmployeeListNeverExposesCredentialsOrMfaState`
- [x] 5.3 `Management/EmployeeManagement` (+ `Allocations`): rename `UsersController` to `EmployeesController`, keeping route `api/users`, and move `UserService` and `UserAllocationService` behind repositories. Verify that `UsersEndpointsTests`, `UserAllocationServiceTests`, `UserDeletionTests`, `LastAdministratorTests` and `RoleAssignmentSafeguardTests` pass. Done: `UserService` is now `EmployeeService` (same name in every layer); test construction goes through `TestServices`

## 6. Module: Communication

- [x] 6.1 `Communication/Mail` (+ `Attachments`): repositories (move the transactional send onto `ExecuteInTransactionAsync`), services and controller. Move `LocalMailFileStore` to `Integrations/Storage`. Verify that the `Mail*Tests` unit tests and `MailEndpointsTests` pass
- [x] 6.2 `Communication/Notification` (+ `Reminders`): repositories for notifications and reminder history, `NotificationService`, `NotificationPublisher`, `ReminderService` and the hosted `ReminderSchedulerService`. `ReminderCalculator`/`ReminderIntervals` stay in `Common/Rules` because tasks, analytics, the dashboard and the assistant use them too (design D2: rules shared across modules go to Common/Rules). Verify that the `Reminder*Tests`, `NotificationServiceTests` and `NotificationsEndpointsTests` pass

## 7. Module: Monitoring

- [x] 7.1 `Monitoring/TaskManagement`: `TaskRepository` with filter records, `TaskService`, `TasksController` and DTOs. Move the single-module rules (`TaskStatusTransitions`, `TaskAccessScope`) to `.../TaskManagement/Rules`; `TaskStageCalculator`, `TaskBucketRules` and `DeadlineBucketRules` stay in `Common/Rules` because analytics and the assistant use them. The manager visibility rule is decided in the service and passed to the repository as a `TaskVisibility` (design D5). Verify that `TaskServiceTests`, `TaskManagementServiceTests`, `TaskDeletionTests`, `TaskLogHoursTests` and `TasksEndpointsTests` pass
- [x] 7.2 `Monitoring/TaskManagement/Discussion` and `.../Activity`: repositories, services and controllers. Verify that `TaskCommentActivityNotificationTests` and `TaskWorkedHoursNotificationTests` pass. Done: the comment and activity endpoints moved from TasksController into TaskCommentsController and TaskActivityController (same routes)
- [ ] 7.3 Add `ITaskAccessService` and rewrite `TaskAccessHandler` to use it (design D7). Verify the missing or deleted task still returns 403 (`GetDetails_MissingOrDeletedTask_IsForbidden`)
- [ ] 7.4 `Monitoring/EmployeeWorkload`: repository, service and controller. Verify that `WorkloadServiceTests`, `CustomRoleWorkloadTests` and `WorkloadEndpointsTests` pass

## 8. Module: Dashboard

- [ ] 8.1 `Dashboard`: `DashboardRepository`, `DashboardService` and controller. Verify that `DashboardServiceTests` and `DashboardEndpointsTests` pass
- [ ] 8.2 `Dashboard/Analytics`: split each `*DashboardScope` into a scope service (allowed ids) and repository filters (design D5), and rewrite the Admin/Manager/Employee analytics services onto analytics repositories. Verify that the `*Analytics*Tests`, `*Scope*Tests`, `AnalyticsDrillDownTests`, `AdminAnalyticsQueryShapeTests` (moved to the repository) and the analytics endpoint and isolation tests pass

## 9. Module: Reports

- [ ] 9.1 `Reports`: repository, service, `ReportCsv` and controller. Verify that `ReportServiceTests`, `ReportCsvTests` and `ReportEndpointsTests` pass

## 10. Modules: Auth and Profile

- [ ] 10.1 `Auth` (+ `Mfa`, `PasswordRecovery`): repositories for users, refresh tokens, MFA backup codes and reset grants. Move `AuthService`, `MfaService` and `PasswordRecovery` onto them. Move `JwtTokenService`, TOTP and `MfaSecretProtector` to `Integrations/Security`. Rewrite `PasswordRecoveryController` with no DbContext. Verify that the `AuthService*Tests`, `MfaServiceTests`, `PasswordRecoveryServiceTests` and the `Auth*`/`Mfa*`/`PasswordRecovery*` endpoint tests pass
- [ ] 10.2 Add `ISessionService` and rewrite `SessionValidation` (and move it to `Middleware/`) to use it. Verify that `AuthenticationWiringTests` and `AuthServiceRefreshAndLogoutTests` pass
- [ ] 10.3 `Profile`: move the own-profile, password and own-MFA endpoints and service methods into the `Profile` module. Verify that `UserProfileEndpointsTests`, `UserServiceSelfServiceTests` and `MfaEnrolmentEndpointsTests` pass

## 11. Module: Assistant

- [ ] 11.1 `Assistant`: conversation, message, pending-action and audit repositories. Move `AssistantService`, `AssistantActionService` and the read/action tools onto services and repositories, with `ChatModelGateway` and the providers in `Integrations/Ai`. Verify that the `Assistant*Tests` unit tests and `AssistantEndpointsTests` pass

## 12. Lock down

- [ ] 12.1 Delete the now-empty `Legacy/` and `Common/Rules` (if empty) folders and `IApplicationDbContext`. Widen the architecture tests to the whole assembly (exempting `Persistence.*`). Verify that `ArchitectureTests` and the full `dotnet test` pass
- [ ] 12.2 Check every module/sub-module path against the design D2 table, and confirm there are no empty folders (`find backend/src -type d -empty`). Update the backend section of `README.md` with the new folder tree and layer rules
- [ ] 12.3 Deploy to the Render dev environment and smoke-test login with MFA, task create/move/comment, the dashboards for all three roles, mail send, and the assistant chat

## Workflow follow-up

- Raise the PR against `master`, review, merge.
- Archive the change with `/opsx:archive` once merged.
