## 1. Preconditions

- [x] 1.1 Confirm login slice (user-auth) builds and runs; verify `dotnet build` succeeds in backend and `npm run build` succeeds in frontend before starting.

## 2. Domain & Data Model

- [x] 2.1 Add `Department` entity to `TaskFlow.Domain/Entities/Department.cs` (Id, Name, Description, CreatedAt, IsDeleted) and verify Domain project builds.
- [x] 2.2 Extend `User` entity in `TaskFlow.Domain/Entities/User.cs` with `DepartmentId` (int?, FK) and navigation `Department?`; add to existing collections as needed; verify Domain builds.
- [x] 2.3 Update `AppDbContext` in `TaskFlow.Infrastructure/Persistence/AppDbContext.cs` to add `Departments` DbSet, configure Department entity (unique index on Name, query filter for IsDeleted), configure relationship User→Department (optional), and keep global filter on User.IsDeleted; verify Infrastructure builds.

## 3. Database Migration

- [x] 3.1 Generate EF Core migration for Departments table and User.DepartmentId FK (e.g. `dotnet ef migrations add AddDepartmentsAndUserDepartment --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.Api`); verify migration files created and compile.
- [x] 3.2 Test migration applies: `dotnet ef database update --project src/TaskFlow.Infrastructure --startup-project src/TaskFlow.Api` creates `departments` table with unique index on Name and FK on users.DepartmentId; verify no errors.

## 4. Application Layer - Departments

- [x] 4.1 Add DTOs in `TaskFlow.Application/Departments/` (`DepartmentDto`, `CreateDepartmentRequest`, `UpdateDepartmentRequest`, `DepartmentWithCountDto`); verify Application builds.
- [x] 4.2 Add validators in `TaskFlow.Application/Departments/Validators/` using FluentValidation (`CreateDepartmentRequestValidator`, `UpdateDepartmentRequestValidator`) enforcing required Name, length, uniqueness check hook via service; verify builds.
- [x] 4.3 Define `IDepartmentService` in `TaskFlow.Application/Departments/IDepartmentService.cs` with methods: GetAllAsync, GetByIdAsync, GetEmployeesAsync, CreateAsync, UpdateAsync, DeleteAsync; verify builds.
- [x] 4.4 Implement `DepartmentService` in `TaskFlow.Application/Departments/DepartmentService.cs` handling unique checks, safe delete (block if employees exist), soft delete, and return counts; verify Application builds and passes unit tests for safe delete/uniqueness.

## 5. Application Layer - Employees (Users)

- [x] 5.1 Add DTOs in `TaskFlow.Application/Users/` (`UserDto`, `CreateUserRequest`, `UpdateUserRequest`, `UserListFilter`, `UserStatusRequest`) including department and role fields; verify builds.
- [x] 5.2 Add validators (`CreateUserRequestValidator`, `UpdateUserRequestValidator`, `UserStatusRequestValidator`) enforcing email/username uniqueness, required fields, role/department validity; verify builds.
- [x] 5.3 Extend `IAuthService` or add `IUserService` in `TaskFlow.Application/Users/IUserService.cs` with methods: GetAllAsync(filter), GetByIdAsync, CreateAsync, UpdateAsync, UpdateStatusAsync, GetOwnAsync; verify builds.
- [x] 5.4 Implement `UserService` in `TaskFlow.Application/Users/UserService.cs` enforcing admin-only where applicable, duplicate checks, deactivation rules; verify builds and unit tests cover duplicate email/username and status change.

## 6. Infrastructure Layer

- [x] 6.1 Register services in DI (`Program.cs` or via extension): add `IDepartmentService`, `IUserService` registrations in Infrastructure/Api composition; verify solution builds.
- [x] 6.2 Ensure DbContext unique indexes and query filters applied correctly; verify build.

## 7. API Layer

- [x] 7.1 Create `DepartmentsController` in `TaskFlow.Api/Controllers/DepartmentsController.cs` with endpoints: GET `/api/v1/departments` (Admin/Manager), POST `/api/v1/departments` (Admin), PUT `/api/v1/departments/{id}` (Admin), DELETE `/api/v1/departments/{id}` (Admin), GET `/api/v1/departments/{id}/employees` (Admin/Manager); return appropriate status codes and ProblemDetails on errors; verify builds.
- [x] 7.2 Create/extend `UsersController` in `TaskFlow.Api/Controllers/UsersController.cs` with endpoints: GET `/api/v1/users` (Admin), GET `/api/v1/users/{id}` (Admin,self), POST `/api/v1/users` (Admin), PUT `/api/v1/users/{id}` (Admin), PATCH `/api/v1/users/{id}/status` (Admin); verify builds and integration tests cover auth/role restrictions.

## 8. Frontend - Core

- [x] 8.1 Add TypeScript models in `frontend/src/app/core/models/` (`department.model.ts`, `user.model.ts`) matching API DTOs; verify build.
- [x] 8.2 Add `DepartmentService` and `UserService` in `frontend/src/app/core/services/` for API calls to departments/users endpoints; verify build and service tests.

## 9. Frontend - Features

- [x] 9.1 Create `DepartmentListComponent` (standalone) in `frontend/src/app/features/departments/` with department list, add/edit form, employee count, delete with confirmation, role-aware actions (Admin only for CUD); verify build and component tests.
- [x] 9.2 Create `EmployeeListComponent` (standalone) in `frontend/src/app/features/employees/` with list, search/filter by name/email/department/role/status, add/edit form, activate/deactivate toggle, role guards; verify build and component tests.

## 10. Routing & Guards

- [x] 10.1 Add routes in `frontend/src/app/app.routes.ts` for `/departments` → `DepartmentListComponent` (RoleGuard: Admin) and `/employees` → `EmployeeListComponent` (RoleGuard: Admin); verify lazy loading/guards work and build.

## 11. End-to-End Verification

- [x] 11.1 Run backend tests: `dotnet test` in `TaskFlow Proj/backend`; all existing and new tests pass.
- [x] 11.2 Run frontend tests: `npm run test` in `frontend`; all tests pass.
- [x] 11.3 Manual verification: Admin can CRUD departments (block delete if employees), Manager can list departments/view employees; Admin can manage employees (create/update/status), prevents duplicates; deactivated user cannot login. Verify per department-management and employee-management specs.