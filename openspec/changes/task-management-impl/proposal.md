## Why

Departments/employees and dashboards are in place. Next core feature is Task Management (Screens 7-8 primarily: Task List, Create Task). Tasks drive assignments, status tracking, deadlines, recipients, comments/activity later - this is central to the system.

## What Changes

- Domain: Task entity, related enums (Priority, Status), TaskRecipient, TaskComment, TaskActivityLog as needed for core flows
- Backend: TaskService with CRUD/listing/filtering, create task (assign to employee, set priority/deadline/start/estimated hours, optional additional recipients), status transitions, validation
- API: endpoints per TDD (list/create/get/update/status/reassign/comments/recipients/activity as applicable for initial slice)
- Frontend: Task List (/tasks), Create Task (/tasks/new) for Manager/Admin; view/update flows
- Migration for tasks tables

## Capabilities

### New Capabilities

- `task-management`: Create, list, view, update tasks with proper RBAC and validation

### Modified Capabilities

_None_

## Impact

- Backend domain/Application/Infrastructure/Api
- Frontend features/tasks
- Database schema additions
- No new external deps
