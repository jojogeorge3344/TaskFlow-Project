# Board View Delta Spec

## ADDED Requirements

### Requirement: Board is a linked sidebar screen
The Board screen SHALL be reachable from the sidebar of every role that has a parent module containing it, while the `/board` route remains open to all authenticated roles.

#### Scenario: Sidebar entry per role
- **WHEN** an Administrator, Manager or Employee opens their sidebar and expands the monitoring/task parent
- **THEN** a **Board** entry (kanban icon) linking to `/board` is listed, and activating it opens the existing Kanban screen.

### Requirement: All status columns share one row

On wide screens the Board SHALL lay out all status columns (To Do, In Progress, In QA, Blocked, Completed) in a single row, wrapping to fewer columns only when the viewport is too narrow to fit them.

#### Scenario: Wide viewport keeps every column on one row

- **WHEN** the Board is opened on a wide (desktop) viewport
- **THEN** all five status columns render side by side in one row, including Completed, with no column dropping to a second row.

### Requirement: Board assignee filter
The Board screen SHALL provide an assignee (user-wise) filter, visible only to roles that can see other users' tasks.

#### Scenario: Administrator/Manager filter by user
- **WHEN** an Administrator or Manager opens the Board and selects a user in the assignee filter
- **THEN** only that user's tasks are shown on the board.

#### Scenario: Employee does not see the assignee filter
- **WHEN** an Employee opens the Board
- **THEN** no assignee filter is shown (their board only contains their own tasks).

### Requirement: Board status filter
The Board screen SHALL provide a status filter covering every task status including In-QA.

#### Scenario: Filter by status
- **WHEN** the user selects a status (e.g. In QA) in the status filter
- **THEN** only tasks in that status are shown, and choosing the empty/"All" value restores all statuses.

### Requirement: Board deadline range filter
The Board screen SHALL provide deadline "from" and "to" date filters that work together as an inclusive range.

#### Scenario: Filter by deadline range
- **WHEN** the user sets a deadline-from and/or deadline-to date
- **THEN** only tasks whose deadline falls within the inclusive range are shown; with both set, tasks outside the range are hidden.

### Requirement: Filter combination and reset
Board filters SHALL combine with AND semantics and be clearable in one action.

#### Scenario: Combined filters
- **WHEN** assignee, status and deadline filters are all set
- **THEN** only tasks matching every active filter are shown.

#### Scenario: Clear filters
- **WHEN** the user activates Clear filters
- **THEN** every filter is reset and the full board is shown again.

#### Scenario: No matches
- **WHEN** no task matches the active filters
- **THEN** a filtered-empty state is shown (distinct from the load-error state).
