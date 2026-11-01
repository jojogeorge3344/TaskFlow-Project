# Proposal

## Why

When a manager creates a task, the Assigned Employee dropdown lists every active employee with nothing to show who belongs to the project or who is busy. The manager has to work out the right person by hand. The original request asked for an automatic pick based on project allocation percentage. In discussion, the stakeholder replaced that with a pick based on current task load: the project team member with the fewest active tasks becomes the default assignee, and the manager can still change it.

## What Changes

- Selecting a project on Create Task (Screen 8) fetches that project's team and each member's current workload, fresh every time the project changes.
- The Assigned Employee dropdown lists **only** employees mapped to the selected project, and each entry shows that employee's workload (active tasks and overdue tasks). **BREAKING (UI):** employees outside the project team can no longer be chosen on Create Task.
- The team member with the fewest active tasks is selected automatically. Ties go to fewer overdue tasks, then to the higher on-time completion rate, then to name A-Z, so the pick is always the same for the same data.
- Allocation percentage plays no part in the pick, and no one is excluded for being "fully allocated". The "All employees are 100% allocated" message from the original request is dropped.
- A project with no mapped employees shows "No employees are mapped to this project." and leaves the assignee empty.
- The manager can override the automatic pick by choosing any other team member in the dropdown.
- A new read-only API returns the assignee candidates for a project, protected by the task-create permission, so any role that can create tasks can use it.
- The uncommitted client-side allocation-based work-in-progress in `task-create.component.*` is replaced by this behaviour.

## Capabilities

### New Capabilities
- `task-assignee-suggestion`: lists a project's assignable team members with their current workload, and picks a default assignee for a new task by fewest active tasks with a fixed tie-break order.

### Modified Capabilities
<!-- None: openspec/specs/ holds no main specs yet. -->

## Impact

- **Backend (TaskFlow.Api):** new `GET /api/v1/tasks/assignee-candidates?projectId={id}` on `TasksController` (permission `tasks.create`), plus a service and repository query that reuse the active/overdue rules from `WorkloadRepository`. Unit and integration tests are added.
- **Frontend:** `task-create.component.{ts,html,spec.ts}` (the existing uncommitted WIP is reworked) and `task.service.ts` / `task.model.ts` (new call and type).
- **Docs:** `TF - Documents/TFWorkflow/.../04.1-Task-Management.md` and `TFTechnicalWorkflow/.../04.1-Task-Management.md`.
- **No schema or migration changes.** Task creation validation on the server is unchanged.
