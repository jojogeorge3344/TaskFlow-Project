## Why

User login is complete (bootstrap + user-auth). The next screens in the flow are Employee Management (Screen 5) and Department Management (Screen 6), which are Admin-only capabilities for organizing people into departments and maintaining employee accounts. These screens and their APIs are required for future task allocation and reporting. Building them now extends the established auth/RBAC, domain, and API patterns from the login slice.

## What Changes

- **Domain model**: Extend `User` to include `DepartmentId` FK to `Department` (matching TDD §7.2), add `Department` entity with Name/Description/CreatedAt/IsDeleted, add navigation properties and indexes. Update `AppDbContext` accordingly.
- **Departments API** (Admin/Manager): `GET /api/v1/departments` (list + counts), `POST /api/v1/departments` (Admin), `PUT /api/v1/departments/{id}` (Admin), `DELETE /api/v1/departments/{id}` (Admin, safe delete - blocked if employees exist), `GET /api/v1/departments/{id}/employees` (Admin, Manager).
- **Employees API** (Admin): `GET /api/v1/users` (list/search/filter by name/email/department/role/status), `GET /api/v1/users/{id}` (Admin, self), `POST /api/v1/users` (Admin), `PUT /api/v1/users/{id}` (Admin), `PATCH /api/v1/users/{id}/status` (Admin - activate/deactivate). Enforce no duplicate email/username, prevent deleting employees with related task records (soft-delete semantics; hard-delete blocked). Employees cannot change own Role/IsActive (covered in profile rules; this slice focuses on admin management).
- **Backend services/validators/controllers** following existing layered architecture (Domain, Application, Infrastructure, Api). Add FluentValidation, DTOs, mapping as needed.
- **EF Core migration** adding `departments` table and `users.DepartmentId` FK with appropriate indexes/constraints.
- **Frontend** (Angular standalone): Admin-only routes `/employees` (EmployeeListComponent with search/filter/add/edit/activate-deactivate) and `/departments` (DepartmentListComponent with list/add/edit/counts/delete guard) per TDD routing. RoleGuard blocks non-Admin access.

## Capabilities

### New Capabilities

- `department-management`: Admin can create/update/list/delete departments safely; Managers can list departments and view department employees; prevent duplicate department names; block deletion when employees exist; display employee counts.
- `employee-management`: Admin can list/filter/search employees (name, email, department, role, status), create/update employees, assign to department and role, activate/deactivate, prevent duplicate email addresses, and avoid accidental deletion when related task records exist.

### Modified Capabilities

_None_ - these are new capabilities.

## Impact

- **Backend**: Domain (add Department, extend User), Application (DepartmentService/UserService interfaces + implementations, DTOs, validators), Infrastructure (AppDbContext, migrations, DI registrations), Api (DepartmentsController, UsersController additions).
- **Frontend**: features/departments (DepartmentListComponent), features/employees (EmployeeListComponent), core services/models if needed, route additions with RoleGuard (Admin).
- **Database**: new `departments` table, `users.DepartmentId` column + FK, unique index on departments.Name, indexes for filtering.
- **Dependencies**: no new packages beyond existing stack.