# Design

## Context

See proposal.md for motivation and specs/task-assignee-suggestion/spec.md for the behaviour.

Current state:

- Create Task (`task-create.component.ts`) loads `GET /api/v1/users?isActive=true` once at page load and filters the list to `role === 'Employee'` for the assignee dropdown. Those users have no task counts.
- There is uncommitted work-in-progress in `task-create.component.{ts,html,spec.ts}` that picks the lowest `totalAllocatedPercent` on the project team on the client. Its copy-flow guard (`copyPending`) and notice signal are worth keeping. Its ranking rule and data source are not.
- `WorkloadRepository` (Screen 16) already holds the agreed definitions: assignable employee = active and the role lacks `tasks.view.all`; active task = not Completed/Cancelled and not deleted; overdue = active with `Deadline < now` (using `IAppClock`). Its endpoint needs `workload.view`, which a task creator may not have.
- `Task` has `Deadline` (non-null) and `CompletedAt` (nullable), so the on-time rate can be computed without schema changes.
- `POST /api/v1/tasks` validates that the assignee is an active user. It does not check project membership.

## Goals / Non-Goals

**Goals:**
- One server call per project change that returns candidates already ranked, so the "who gets picked" rule lives in one place and is unit-testable in C#.
- Reuse the workload screen's counting rules exactly, so the numbers on Create Task match Screen 16.

**Non-Goals:**
- Enforcing team membership on `POST /api/v1/tasks` or on reassign. The team-only restriction is a Create Task UI rule. Adding a server rule would change the reassign flow and the AI assistant's task tools, so it is out of scope.
- Weighting by priority, estimated hours or skills. "Efficiency" means only the tie-breakers in the spec.
- Changing the Reassign dialog.

## Decisions

**D1 - New endpoint `GET /api/v1/tasks/assignee-candidates?projectId={id}` on `TasksController`, permission `tasks.create`.**
It sits under `/tasks` because it serves task creation and is guarded by the same permission as `POST /tasks`. The literal segment does not clash with the `{id:int}` routes.
*Alternatives:* reuse `/workload?projectId=` (403 for roles without `workload.view`, and no on-time rate); `/projects/{id}/assignee-candidates` (would naturally sit under `projects.view`, the wrong guard).

**D2 - Ranking happens on the server, and the client takes the first item.**
The response is ordered by: active ASC, overdue ASC, on-time rate DESC with nulls last, full name ASC (ordinal-ignore-case), id ASC. The client preselects `candidates[0]` and does no ranking of its own.
*Alternative:* return unordered data and rank on the client (the current WIP approach). That splits the rule across two codebases and makes C# tests unable to cover it.

**D3 - Query shape: two queries, ranking in memory.**
1. Candidates: users on the project's allocations, active, role without `tasks.view.all`, projected to id, name and code.
2. Counts per candidate in one grouped query over non-deleted tasks: active, overdue, completed, and completed on time (`CompletedAt <= Deadline`).

Merge and sort in C#. Teams are small (tens of people), so sorting in memory is cheap and keeps the null-last rate logic readable. Put the counts query next to the workload one, or extract the shared predicates, so "active" and "overdue" stay defined once.
*Alternative:* a single LINQ query with the sort translated to SQL. The nullable-rate ordering gets awkward and may not translate cleanly.

**D4 - Response DTO** `AssigneeCandidateDto(int UserId, string FullName, string? EmployeeCode, int ActiveTasks, int OverdueTasks, double? OnTimeRate)`.
`OnTimeRate` is in 0..1 and is null when there are no completed tasks. A missing or deleted project returns 404 (checked first, so an empty team gives `200 []`, not 404).

**D5 - Client flow: `projectId.valueChanges` → `switchMap` → candidates request.**
`switchMap` cancels a stale request, which satisfies the out-of-order scenario. The component keeps a `candidates` signal and a `candidatesState` (`idle | loading | loaded | error`). The dropdown renders from `candidates`, and the option text is `Name (CODE) - N active - M overdue`, with the overdue part omitted when it is 0.
The page-load `getAll` users call stays, because recipients still need all active users. It no longer feeds the assignee dropdown.

**D6 - Override and notice behaviour.**
The auto-pick runs only when a candidates response lands, which happens after a project change. A manual selection is therefore never overwritten until the project changes. The hint is shown while the selected id equals the auto-picked id, as the WIP already does. Notices:
- picked: hint "Auto-selected: fewest active tasks on this project's team (N active)."
- empty team: warning "No employees are mapped to this project."
- error: warning "This project's team could not be loaded."

**D7 - Copy flow.**
While a copy is pending, the first candidates response for the copied project keeps the copied assignee if their id is in the list. Otherwise it applies the auto-pick and appends "The original assignee is no longer on this project, so the least-loaded team member was selected." to the copy notice. This reuses the WIP's `copyPending` guard.

**D8 - Dropping the WIP's allocation logic.**
`suggestAssignee`, `AssigneeSuggestion`, the `MAX_TOTAL_PERCENT` import and the allocation-based specs are removed, not adapted. The ranking is now on the server and allocation no longer matters.

## Risks / Trade-offs

- [The team-only dropdown hides a manager's usual pick for a project whose team list is out of date] → The empty-team message points at the cause. Team membership is maintained on the Project / Employee screens. This trade-off was accepted in discussion.
- [The UI restricts to the team, but the API still accepts any active assignee] → Documented as a non-goal. If needed, server enforcement can follow as its own change covering create and reassign together.
- [Counts can change between the form loading candidates and the user saving] → Acceptable: the pick is a suggestion, and the data is refetched on every project change, as the spec requires.
- [Penalising employees with no completed tasks in ties may disadvantage new joiners] → This applies only after active and overdue counts are equal. It is a deliberate "proven track record first" choice recorded in the spec, and easy to flip later.
- [Assignable-employee rule changes from `role === 'Employee'` to "role lacks `tasks.view.all`"] → This matches the server's workload definition and covers custom employee-like roles. Default roles behave the same as today.

## Migration Plan

No data migration. Deploy backend and frontend together. Rollback means reverting both, and nothing persistent is affected.
