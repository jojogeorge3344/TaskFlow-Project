# Spec Delta

## Purpose

Enable Managers and Administrators to create tasks and assign them to employees, and let authorized users find, filter, and open the tasks they may access.

## ADDED Requirements

### Requirement: Task creation
The system SHALL allow Managers and Administrators to create a task with a title, assigned employee, deadline, optional description, priority, start date, and estimated hours. The system MUST assign each task a unique task code and MUST record the task's creator and creation timestamp.

#### Scenario: Manager creates a task
- **WHEN** a Manager submits a task with a title, a valid assignee, a start date, a deadline later than the start date, a positive estimated duration, and a valid priority
- **THEN** the system creates the task, assigns it to the selected employee, generates a unique task code, and records the creator and creation timestamp

#### Scenario: Administrator creates a task
- **WHEN** an Administrator submits a valid task
- **THEN** the system creates the task successfully

#### Scenario: Employee cannot create a task
- **WHEN** an Employee attempts to create a task
- **THEN** the system responds with forbidden and creates no task

### Requirement: Task creation validation
The system SHALL reject a task creation request that omits a required field or violates a field rule, returning field-level errors and creating no task.

#### Scenario: Missing required field
- **WHEN** a task is submitted without a title, assigned employee, or deadline
- **THEN** the system returns validation errors and creates no task

#### Scenario: Deadline not after start date
- **WHEN** a task is submitted with a deadline earlier than or equal to its start date
- **THEN** the system rejects the request with a validation error for the deadline

#### Scenario: Non-positive estimated duration
- **WHEN** a task is submitted with an estimated duration of zero or less
- **THEN** the system rejects the request with a validation error for the estimated duration

### Requirement: Task priority selection
The system SHALL accept a task priority of Low, Medium, High, or Critical and MUST reject any other value.

#### Scenario: Valid priority stored
- **WHEN** a task is created with priority High
- **THEN** the task is stored with priority High

#### Scenario: Invalid priority rejected
- **WHEN** a task is submitted with a priority outside the allowed set
- **THEN** the system rejects the request with a validation error

### Requirement: Task recipients
The system SHALL always include the assigned employee among a task's notification recipients and SHALL allow the creator or an authorized Manager to add additional recipients. Recipients MUST be unique per task.

#### Scenario: Assignee always a recipient
- **WHEN** a task is created or reassigned
- **THEN** the assigned employee appears among the task's notification recipients

#### Scenario: Additional recipients captured
- **WHEN** a Manager creates a task with additional recipients
- **THEN** the system stores those recipients alongside the assignee

#### Scenario: Duplicate recipient collapsed
- **WHEN** an additional recipient is also the assignee
- **THEN** the system stores a single recipient entry for that user

### Requirement: Task reminder intervals
The system SHALL allow the creator to choose, from predefined options, when reminders are sent for a task before and after its deadline.

#### Scenario: Reminder intervals captured
- **WHEN** a Manager creates a task and selects reminder intervals
- **THEN** the system records the selected intervals for the task

#### Scenario: Default intervals applied
- **WHEN** a Manager creates a task without changing the reminder options
- **THEN** the system applies the default reminder intervals

### Requirement: Task creation side effects
The system SHALL, when a task is created, record an initial activity-history entry and notify the assigned employee that the task was assigned.

#### Scenario: Initial activity recorded
- **WHEN** a task is created
- **THEN** the system records a creation activity entry that names the creator and the recorded values

#### Scenario: Assignee notified
- **WHEN** a task is assigned to an employee
- **THEN** the system creates an in-app notification for that employee

### Requirement: Task listing
The system SHALL list the tasks a user is authorized to see, showing each task's code, title, assignee, creator, priority, start date, deadline, estimated duration, and status.

#### Scenario: Assignee sees own tasks
- **WHEN** an Employee lists tasks
- **THEN** the system returns only tasks they are assigned to or otherwise related to

#### Scenario: Manager lists tasks in scope
- **WHEN** a Manager lists tasks
- **THEN** the system returns the tasks they are authorized to access

#### Scenario: List is paginated
- **WHEN** a user lists tasks
- **THEN** the system returns a page of results with the total count and page metadata

### Requirement: Task search
The system SHALL allow searching the task list by task code or title.

#### Scenario: Search matches code or title
- **WHEN** a user searches with text matching a task's code or title
- **THEN** the system returns the matching tasks

#### Scenario: Search with no matches
- **WHEN** a user searches with text matching no task
- **THEN** the system returns an empty result set

### Requirement: Task filters
The system SHALL allow filtering the task list by assigned employee, status, priority, and deadline.

#### Scenario: Filter by employee and status
- **WHEN** a user filters the list by an assigned employee and a status
- **THEN** the system returns only tasks matching both filters

#### Scenario: Filter by deadline range
- **WHEN** a user filters the list by a deadline range
- **THEN** the system returns only tasks whose deadline falls within the range

### Requirement: Task deadline indicators
The system SHALL mark overdue tasks and surface upcoming deadlines in the task list.

#### Scenario: Overdue task marked
- **WHEN** a task not in a terminal status has a deadline in the past
- **THEN** the list marks the task as overdue

#### Scenario: Upcoming deadline shown
- **WHEN** a task not in a terminal status has a deadline in the future
- **THEN** the list shows the deadline as upcoming

### Requirement: Open task details
The system SHALL allow a user to open a task's details from the list when they are authorized to view that task.

#### Scenario: Open authorized task
- **WHEN** a user opens a task from the list that they may access
- **THEN** the system shows that task's details

#### Scenario: Open unauthorized task denied
- **WHEN** a user attempts to open a task they may not access
- **THEN** the system denies access

### Requirement: Task details
The system SHALL allow users with task access to view a task's full details, including its derived stage and current recipients.

#### Scenario: Authorized user views task
- **WHEN** a user with access views a task
- **THEN** the system returns the task's full details

### Requirement: Status updates
The system SHALL allow the assignee and Managers to change a task's status between allowed values and MUST reject a disallowed transition.

#### Scenario: Assignee updates own task status
- **WHEN** the assignee changes their task's status to an allowed next value
- **THEN** the system persists the status change

#### Scenario: Disallowed transition rejected
- **WHEN** a user requests a status change that the allowed transitions do not permit
- **THEN** the system rejects the request and leaves the status unchanged

### Requirement: Task modification authorization
The system SHALL prevent users without management rights from changing a task's assignment or deadline.

#### Scenario: Employee cannot change assignment or deadline
- **WHEN** an Employee attempts to change a task's assignee or deadline
- **THEN** the system responds with forbidden and the task is unchanged
