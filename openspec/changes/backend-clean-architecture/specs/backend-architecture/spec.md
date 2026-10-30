# Spec Delta

## Purpose

Defines the structural rules the TaskFlow backend must follow (layered Clean Architecture with the Repository Pattern and a module-based folder layout) so the codebase stays navigable and each rule can be checked automatically.

## ADDED Requirements

### Requirement: One-way layered flow
The backend SHALL be organised into a controller layer, a service layer, a repository layer and the database. Requests SHALL flow only Controller → Service → Repository → Database, and each layer SHALL call only the layer directly below it.

#### Scenario: Controller does not reach a repository or the database
- **WHEN** the architecture test suite inspects every type in the controller layer
- **THEN** no controller type depends on a repository type, the database context, or the EF Core API

#### Scenario: Service does not reach the database
- **WHEN** the architecture test suite inspects every type in the service layer
- **THEN** no service type depends on the database context or the EF Core API

#### Scenario: Request pipeline components respect the layers
- **WHEN** the architecture test suite inspects middleware, filters and authorization handlers
- **THEN** none of them depends on the database context. Any data they need comes through a service

### Requirement: Controllers handle HTTP only
Controllers SHALL only bind requests, delegate to one or more services, and translate the result into an HTTP response. They SHALL contain no business rules, no data access and no per-action exception handling.

#### Scenario: No exception handling in controllers
- **WHEN** the architecture test suite scans controller source files
- **THEN** no controller action contains a `try`/`catch` block

#### Scenario: No manual validation in controllers
- **WHEN** the architecture test suite inspects controller dependencies
- **THEN** no controller depends on a validator type

### Requirement: Services depend on abstractions
Services SHALL contain the business rules, including who may see or change which data, and SHALL depend on repositories only through repository interfaces.

#### Scenario: Service constructor takes interfaces only
- **WHEN** the architecture test suite inspects every service's constructor parameters
- **THEN** every repository dependency is an interface from the repository interfaces folder, never a concrete repository class

#### Scenario: Visibility rules live in services
- **WHEN** a manager requests dashboard analytics
- **THEN** the service decides which employees and projects are in scope and passes them to the repository as filter values, and the response is the same as before the restructure

### Requirement: Repositories handle data access only
Repositories SHALL perform all queries and persistence and SHALL contain no business rules. A repository SHALL NOT return a deferred query object, and SHALL NOT commit changes. Commits and transactions SHALL be owned by a unit of work that services call.

#### Scenario: No deferred queries leave a repository
- **WHEN** the architecture test suite inspects the public members of every repository interface
- **THEN** no member returns `IQueryable` or another deferred query type

#### Scenario: Commit happens once per use case
- **WHEN** a service completes a write that touches several repositories
- **THEN** all changes are committed together through the unit of work, or none are if any step fails

### Requirement: Dependency injection wires every layer
Controllers, services, repositories and the unit of work SHALL be registered with the dependency-injection container and SHALL receive their dependencies through constructors.

#### Scenario: Every interface has a registration
- **WHEN** the application starts in the test host
- **THEN** every service interface and repository interface resolves to an implementation without error

### Requirement: DTOs are separate from entities
Request and response types SHALL be DTOs kept apart from database entities. No controller action SHALL accept or return an entity type.

#### Scenario: Entities never cross the HTTP boundary
- **WHEN** the architecture test suite inspects every controller action's parameters and return types
- **THEN** none of them is, or contains, an entity type

### Requirement: Layer-first folder structure
The backend SHALL keep each layer in a dedicated top-level folder (controllers, services, repositories, models, DTOs, middleware, config, common), with service and repository interfaces in an `Interfaces` sub-folder of their layer.

#### Scenario: Type lives in its layer's folder
- **WHEN** the architecture test suite inspects every controller, service and repository type
- **THEN** each one's namespace begins with its layer's folder name

### Requirement: Module hierarchy mirrors the permission catalog
Inside each layer, code SHALL be grouped by module and then sub-module, following the module tree shown in Role Configuration, plus an `Auth` module for flows that run before sign-in. A section of a sub-module SHALL get its own nested folder only when it has its own controller or service. No empty placeholder folders SHALL exist.

#### Scenario: Same path across layers
- **WHEN** a sub-module has a controller, a service and a repository
- **THEN** all three live under the same module/sub-module path in their respective layers (for example `Monitoring/TaskManagement`)

#### Scenario: Section with its own service is nested
- **WHEN** a section such as task discussion has its own service
- **THEN** its controller, service and repository live in a nested section folder (for example `Monitoring/TaskManagement/Discussion`)

### Requirement: Consistent naming
Types SHALL be named `<Name>Controller`, `<Name>Service` / `I<Name>Service`, and `<Name>Repository` / `I<Name>Repository`, using the same `<Name>` for one sub-module in every layer.

#### Scenario: Naming check
- **WHEN** the architecture test suite inspects types in each layer folder
- **THEN** every controller type ends with `Controller`, every service type with `Service`, and every repository type with `Repository`

### Requirement: Shared code in a common folder
Code used by more than one module (exceptions, constants, helpers, base classes such as the base repository) SHALL live in the `Common` folder, not inside a module.

#### Scenario: Base repository location
- **WHEN** a developer looks for the shared repository base class or the application exception types
- **THEN** they are under `Common`

### Requirement: HTTP contract preserved
The restructure SHALL NOT change any HTTP route, method, request payload or success response payload.

#### Scenario: Existing endpoint tests still pass
- **WHEN** the integration test suite runs after the restructure
- **THEN** every endpoint test passes, except error-body assertions changed as described in the api-error-handling capability
