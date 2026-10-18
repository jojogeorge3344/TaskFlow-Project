# Task Status Delta Spec

## ADDED Requirements

### Requirement: In-QA status exists
The system SHALL support a task status `In-QA` in addition to To Do, In Progress, Blocked, Completed and Cancelled, available everywhere statuses are displayed or selected (task lists, details, board columns, reports filters).

#### Scenario: In-QA appears in status pickers and board
- **WHEN** a user views task status pickers, filters or the Kanban board
- **THEN** In-QA ("In QA") is offered/placed between In Progress and Blocked.

### Requirement: In-QA transitions
Status transitions into and out of In-QA SHALL follow the workflow: In Progress → In-QA, In-QA → In Progress (QA send-back), In-QA → Completed, and In-QA → Blocked/Cancelled. To Do cannot transition directly to In-QA, and Completed/Cancelled remain terminal.

#### Scenario: Move a task into In-QA
- **WHEN** a user requests a status change from In Progress to In-QA
- **THEN** the transition is accepted.

#### Scenario: Send a task back from In-QA
- **WHEN** a user requests a status change from In-QA to In Progress
- **THEN** the transition is accepted.

#### Scenario: Complete from In-QA
- **WHEN** a user requests a status change from In-QA to Completed
- **THEN** the transition is accepted and completion side effects apply as for any completion.

#### Scenario: Disallowed direct jump
- **WHEN** a user requests a status change from To Do directly to In-QA
- **THEN** the request is rejected by the same validation that governs other invalid transitions.

### Requirement: In-QA counts as active
In-QA SHALL be treated as an open/active status everywhere active tasks are counted or classified (workload, dashboards, reports, overdue logic): not completed, not cancelled.

#### Scenario: Workload and dashboard counts
- **WHEN** a task in In-QA exists for a user
- **THEN** it counts as an open/active task (and overdue if past its deadline), never as completed.
