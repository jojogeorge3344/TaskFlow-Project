## Context

Build core task management. Need Task entity with enums (Priority, Status), basic services, controllers, and frontend list/create.

## Goals / Non-Goals

**Goals:** Task domain model, CRUD API, Task List/Create UI for Manager/Admin.
**Non-Goals:** Comments, recipients management UI beyond basic, Kanban board, reassign details, activity history beyond what comes later.

## Decisions

| Decision | Rationale | Alternatives |
|---|---|---|
| D1: TaskStatus enum values match spec (ToDo, InProgress, Blocked, Completed, Cancelled) | Aligns with TDD | Different naming - inconsistent |
| D2: TaskPriority (Low/Medium/High/Critical) | From TDD | Fewer levels - less expressive |
| D3: Soft delete for tasks | Historical integrity | Hard delete - breaks history/audit |
| D4: Manager/Admin create; access policy enforced | Matches RBAC | Broader access - violates least privilege |

## Risks / Trade-offs

| Risk | Mitigation |
|---|---|
| Overdue computation derived at query time | Store deadline/status, compute on read (no stale flags) |
| Recipients model - keep minimal for now | Add TaskRecipient entity but defer full UI |

## Migration Plan

1. Add enums and Task entity (+ related minimal entities)
2. Migration
3. Application services/DTOs/validators
4. API controllers
5. Frontend components
6. Tests
