## Context

See proposal.md for motivation. This design extends the existing auth scaffold (User/Role, AppDbContext, layered architecture, validation, ProblemDetails, RBAC policies) to support department and employee management as specified in TDD §7-§8.3 and Screens 5-6.

The current codebase has:
- `User` entity (no Department navigation/Id), `Role` enum
- `AppDbContext` with global query filter for `IsDeleted`
- Layered: Domain → Application → Infrastructure → Api
- JWT auth, `PasswordHasher<User>`, FluentValidation registered
- Angular standalone with AuthGuard/RoleGuard

## Goals / Non-Goals

**Goals:**
- Add `Department` entity and extend `User` with `DepartmentId`/navigation
- Implement Departments API (list/create/update/delete with safe-guard, view employees) with Admin/Manager access as appropriate
- Implement Employees API (list/search/filter, get, create, update, status change) with Admin-only where specified; preserve auth constraints (deactivated cannot login, generic errors)
- Frontend components for Admin-only screens `/employees` and `/departments` with search/filters/actions
- EF Core migration adding departments table and FK/indexes; follow soft-delete and unique constraints

**Non-Goals:**
- Password change/reset (covered elsewhere), self-update beyond viewing (screen 19 rules not fully in scope)
- Task reassignment/workload/reports (separate features)
- UI polish beyond functional parity with screen requirements

## Decisions

| Decision | Rationale | Alternatives considered |
|---|---|---|
| D1: Add `Department` to Domain with soft delete | Matches history-protection (BR-18) and existing pattern (`IsDeleted`, global filter) | Hard delete - rejected as it breaks historical references |
| D2: Extend `User` with `DepartmentId` (nullable FK) | Departments are organizational; employees may be unassigned initially? TDD shows nullable FK to departments | Non-null - too restrictive for initial data |
| D3: Department Name unique (case-insensitive semantics at validation) | Prevents duplicate department names | Allow duplicates - rejected per requirements |
| D4: Safe delete - block if any users in department exist | Prevents accidental deletion when employees assigned | Cascade soft-delete users - not desired, breaks audit/history |
| D5: Employees API scoped to Admin for CUD; read self allowed | Follows RBAC (Admin manages employees; self can view own profile) | Manager can edit employees - rejected per Screen 5 (Admin only) |
| D6: Deactivated accounts (`IsActive=false`) cannot login - enforce in auth (already exists) | Consistent with user-auth requirements | Separate check here - redundant, auth layer owns it |
| D7: Use existing layered services pattern (UserService, DepartmentService) + interfaces in Application | Maintains consistency with AuthService | Direct EF in controllers - breaks layering |
| D8: Angular standalone components with RoleGuard (Admin) | Matches login implementation and routing model | Module-based - inconsistent with current frontend |
| D9: Return employee counts with departments list | Matches "Department employee count" element | Separate endpoint - extra round trips |

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| Migration may fail if existing users have inconsistent state | Create departments table first, add FK nullable; seed carefully; test on clean DB |
| Email/username uniqueness enforced in DB + validation | Use EF unique indexes (as in users) and FluentValidation to give clear errors |
| Frontend state management grows | Keep simple signals + HTTP services; follow existing auth service pattern |
| Safe delete race condition (concurrent adds) | Check employee count inside transaction before delete |
| Department name uniqueness case sensitivity | Enforce via normalized check (ToLower/Trim) in validation/service |

## Migration Plan

1. Add `Department` entity to Domain
2. Extend `User` with `DepartmentId` and navigation
3. Update `AppDbContext` with Department DbSet, relationships, unique index on Name, query filter
4. Create EF Core migration (add departments table, add users.DepartmentId FK)
5. Apply migration (dev via MigrateAsync on startup, consistent with existing)
6. Implement backend services/controllers with validation
7. Implement frontend components/routes

No data loss expected as FK is nullable and existing users unchanged.
