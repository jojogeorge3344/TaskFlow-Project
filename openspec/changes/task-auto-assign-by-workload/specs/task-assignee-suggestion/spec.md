# Spec Delta

## Purpose

Helps a manager pick who should take a new task by listing the selected project's team with each member's current workload, and preselecting the least-loaded member as the default assignee.

## ADDED Requirements

### Requirement: Assignee candidates are the project's team
When a project is selected on Create Task, the Assigned Employee dropdown SHALL list only the active, assignable employees mapped to that project. Assignable means an employee whose role cannot view all tasks, the same rule the Employee Workload screen uses. No other employees SHALL appear.

#### Scenario: Only mapped employees are listed
- **WHEN** the user selects project P, whose team is A, B and C, while employee D is active but not on P
- **THEN** the dropdown lists A, B and C and does not list D

#### Scenario: Inactive team members are excluded
- **WHEN** the selected project's team includes an employee whose account is inactive
- **THEN** that employee does not appear in the dropdown

#### Scenario: No project selected
- **WHEN** no project is selected
- **THEN** the dropdown lists no employees and shows a prompt to select a project first

### Requirement: Each candidate shows current workload
Each employee in the Assigned Employee dropdown SHALL show their current workload: the number of active tasks and the number of overdue tasks assigned to them across all projects. Active means not Completed and not Cancelled. Overdue means active with a deadline in the past.

#### Scenario: Workload appears in the option
- **WHEN** employee A has 3 active tasks, 1 of them overdue
- **THEN** A's dropdown entry shows A's name and code together with "3 active" and "1 overdue"

#### Scenario: Employee with no tasks
- **WHEN** employee B has no active tasks
- **THEN** B's dropdown entry shows "0 active"

### Requirement: Least-loaded team member is selected automatically
When the candidates for the selected project have loaded, the system SHALL preselect the candidate with the fewest active tasks. Allocation percentage SHALL NOT affect the pick, and no candidate SHALL be excluded because of their allocation percentage.

#### Scenario: Fewest active tasks wins
- **WHEN** the team is A (5 active), B (2 active), C (4 active) and D (3 active)
- **THEN** B is selected automatically

#### Scenario: Allocation percentage is ignored
- **WHEN** employee A is 100% allocated with 1 active task and employee B is 20% allocated with 4 active tasks
- **THEN** A is selected automatically

#### Scenario: A hint explains the pick
- **WHEN** a candidate has been selected automatically
- **THEN** a hint under the field says the employee was auto-selected for having the fewest active tasks on the project team

### Requirement: Ties are broken consistently by efficiency
When two or more candidates share the fewest active tasks, the system SHALL choose among them in this order: fewer overdue tasks; then higher on-time completion rate (completed tasks finished on or before their deadline, divided by all completed tasks); then full name A-Z; then lowest user id. A candidate with no completed tasks SHALL rank after any tied candidate who has a rate.

#### Scenario: Fewer overdue breaks the tie
- **WHEN** A and B each have 2 active tasks, A has 1 overdue and B has 0 overdue
- **THEN** B is selected

#### Scenario: On-time rate breaks the tie
- **WHEN** A and B each have 2 active tasks and 0 overdue, A finished 9 of 10 completed tasks on time and B finished 6 of 10
- **THEN** A is selected

#### Scenario: No completed history ranks last among ties
- **WHEN** A and B are tied on active and overdue tasks, A has no completed tasks and B has an on-time rate of 50%
- **THEN** B is selected

#### Scenario: Full tie falls back to name
- **WHEN** A ("Anita") and B ("Bala") are tied on every workload measure
- **THEN** Anita is selected, and the same choice is made every time for the same data

### Requirement: Manual override of the assignee
The user SHALL be able to replace the automatically selected employee with any other candidate in the dropdown. The automatic pick SHALL NOT overwrite a manual choice unless the project changes.

#### Scenario: User picks someone else
- **WHEN** B was auto-selected and the user chooses C from the dropdown
- **THEN** C stays selected, the auto-selected hint is no longer shown, and the task is created with C as assignee

### Requirement: Candidates refresh when the project changes
Every time the selected project changes, the system SHALL fetch that project's candidates and workload again from the server, then reapply the automatic pick. A response for a project that is no longer selected SHALL be discarded.

#### Scenario: Switching projects
- **WHEN** the user selects project P, then changes to project Q
- **THEN** the dropdown shows Q's team with current workload, and Q's least-loaded member is selected in place of any earlier choice

#### Scenario: Latest data is used
- **WHEN** an employee is assigned a new task elsewhere after the form was opened, and the user then selects a project they belong to
- **THEN** the counts shown and the automatic pick include that new task

#### Scenario: Out-of-order responses
- **WHEN** the user changes from P to Q before P's candidates have arrived
- **THEN** only Q's candidates are shown and used

### Requirement: Project with no team
When the selected project has no assignable employees mapped to it, the system SHALL leave the assignee empty and show "No employees are mapped to this project."

#### Scenario: Empty team
- **WHEN** the user selects a project with no mapped active employees
- **THEN** the dropdown has no employees, no one is selected, and the message "No employees are mapped to this project." is shown

### Requirement: Candidate loading failure
If the candidates for the selected project cannot be loaded, the system SHALL leave the assignee empty and show a message that the project's team could not be loaded, without blocking the rest of the form.

#### Scenario: Server error
- **WHEN** the candidate request fails
- **THEN** no assignee is selected and a message says the project's team could not be loaded

### Requirement: Copied task keeps its assignee when possible
When Create Task is opened from "Copy task", the copied assignee SHALL be kept if they are a candidate for the copied project. Otherwise, the automatic pick SHALL apply and the copy notice SHALL say the original assignee could not be carried over.

#### Scenario: Copied assignee still on the team
- **WHEN** a task assigned to A on project P is copied and A is still on P's team
- **THEN** A is selected, not the least-loaded member

#### Scenario: Copied assignee no longer on the team
- **WHEN** a task assigned to A on project P is copied and A is no longer on P's team
- **THEN** the least-loaded member of P is selected and the copy notice says the original assignee was not carried over

### Requirement: Assignee candidates API
The system SHALL expose a read-only endpoint that returns the assignee candidates for a project, each with id, full name, employee code, active task count, overdue task count and on-time completion rate (null when there is no history), ordered by the tie-break rule. It SHALL require the task-create permission.

#### Scenario: Authorised request
- **WHEN** a user with the task-create permission requests the candidates for an existing project
- **THEN** the response is 200 with the project's candidates in pick order, with the first item being the one the form preselects

#### Scenario: Missing permission
- **WHEN** a user without the task-create permission calls the endpoint
- **THEN** the response is 403

#### Scenario: Unknown project
- **WHEN** the project id does not exist or is deleted
- **THEN** the response is 404
