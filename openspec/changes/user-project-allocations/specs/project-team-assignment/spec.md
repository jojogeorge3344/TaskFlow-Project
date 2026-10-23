## ADDED Requirements

### Requirement: A new project offers team assignment
After a project is created, the project form SHALL offer an "Assign team" step. The step SHALL be skippable, and a project SHALL be valid with no members.

#### Scenario: Assign right after create
- **WHEN** an assigner creates a project
- **THEN** the form moves to the "Assign team" step

#### Scenario: Skip
- **WHEN** the assigner skips the step
- **THEN** the project exists with no members and members can be added later from its member list

### Requirement: Employees are chosen from a searchable list
Each team row SHALL select an employee from a dropdown that can be searched by typing, and SHALL show the employee's remaining allocation percentage.

#### Scenario: Search
- **WHEN** the assigner types part of a name
- **THEN** the dropdown lists only matching employees, each with the percentage they still have available

#### Scenario: Fully allocated employee
- **WHEN** an employee has 0% remaining
- **THEN** they are shown as fully allocated and saving them with any percentage is refused by the server

### Requirement: Each team member has a position and a percentage
Each team row SHALL have a position and a whole percentage from 1 to 100. The position SHALL be chosen from the existing positions or typed as a new name.

#### Scenario: Pick an existing position
- **WHEN** the assigner picks QA for a row
- **THEN** the member is saved with position QA

#### Scenario: Type a new position
- **WHEN** the assigner types a position name that does not exist, such as "Scrum Master"
- **THEN** the position is created and available in the list for later rows and projects

#### Scenario: Duplicate name ignoring case
- **WHEN** the assigner types "qa" while "QA" exists
- **THEN** the existing QA position is used and no second position is created

#### Scenario: Several members
- **WHEN** the assigner adds several rows with different employees, positions and percentages
- **THEN** all are saved to the project together

### Requirement: The 100 percent cap applies from the project side
Saving team members SHALL go through the same allocation rule as the employee form: no employee's total across all projects may exceed 100%. If any row would break the cap, the whole request SHALL be rejected and nothing saved.

#### Scenario: One row over the cap
- **WHEN** one of several rows would push its employee above 100%
- **THEN** the request is rejected, the message names that employee with their total and remaining percentage, and no row is saved

### Requirement: Members can be edited later
The project's member list SHALL show each member with position and percentage, and SHALL let an assigner add, change and remove members using the same row editor.

#### Scenario: Change a percentage
- **WHEN** an assigner changes a member's percentage from 40 to 50 and the employee stays within 100%
- **THEN** the change is saved and shown in the member list and on the employee's own screen

#### Scenario: Remove a member
- **WHEN** an assigner removes a member from the project
- **THEN** that allocation is deleted and the employee's remaining percentage increases

### Requirement: A position is a label only
A position SHALL NOT grant permissions. In particular, the "Manager" position SHALL NOT make the user a manager of the project for oversight purposes.

#### Scenario: Manager position
- **WHEN** an employee is given the position Manager on a project
- **THEN** their access and the project's manager oversight links are unchanged
