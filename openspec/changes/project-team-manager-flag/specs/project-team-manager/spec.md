# Spec Delta

## Purpose

Lets an administrator choose a project's managers while assigning its team, and makes that choice the single source for who manages the project, whose manager each team member has, and which people a manager can see.

## ADDED Requirements

### Requirement: Team members can be flagged as project managers
Each person on a project's team SHALL carry a Manager flag that the administrator can set or clear in the team dialog, and more than one person may be flagged.

#### Scenario: Flag a team member
- **WHEN** an administrator assigns a team and marks a person as Manager
- **THEN** that person is saved as a manager of the project, with their position and percentage

#### Scenario: Several managers
- **WHEN** an administrator marks two people as Manager on the same project
- **THEN** both are saved as managers of the project

### Requirement: A team with members must have a manager
Saving a project team that has at least one member SHALL be refused unless at least one member is flagged as Manager, in both the dialog and the API.

#### Scenario: No manager flagged
- **WHEN** an administrator tries to save a team of two people and neither is flagged
- **THEN** the dialog shows that a manager is required and does not save
- **AND** the API rejects the same request with a validation error and changes nothing

#### Scenario: Empty team
- **WHEN** an administrator saves a team with no members
- **THEN** the save succeeds and the project has no managers

#### Scenario: Skipping after creating a project
- **WHEN** an administrator skips the team dialog right after creating a project
- **THEN** the project exists with no team and no manager

### Requirement: Only people with the Manager role can be flagged
A person SHALL be accepted as a project manager only if their role is Manager.

#### Scenario: Flag is unavailable for other roles
- **WHEN** the team dialog shows a team member whose role is not Manager
- **THEN** the Manager flag for that row is disabled with a short explanation

#### Scenario: API rejects a non-manager
- **WHEN** a request flags a person whose role is not Manager
- **THEN** the API rejects it and saves nothing

### Requirement: Flags define the manager link
Saving a team SHALL set the "manager oversees this project" link for exactly the flagged people and remove it for everyone else on that project, and removing a flagged person from the team SHALL remove their link.

#### Scenario: Link follows the flag
- **WHEN** a team is saved with Maya flagged and Ravi not flagged
- **THEN** Maya oversees the project and Ravi does not

#### Scenario: Replacing the manager
- **WHEN** a team is saved again with Ravi flagged and Maya no longer flagged
- **THEN** Ravi oversees the project and Maya no longer does

#### Scenario: Deleting a project
- **WHEN** a project is deleted
- **THEN** its manager links are removed as before

### Requirement: Employees get the project manager as a manager
A person's managers SHALL include the flagged managers of every project they are on, in addition to the managers of their departments, without duplicates, and a manager's reports SHALL include everyone on the projects they manage.

#### Scenario: Employee view
- **WHEN** an employee is on a project whose manager is Maya
- **THEN** Maya appears among that employee's managers

#### Scenario: Manager view
- **WHEN** Maya manages a project with three other people
- **THEN** those three appear among Maya's reports

#### Scenario: Already a department manager
- **WHEN** the same person is both a department manager and a project manager for an employee
- **THEN** they are listed once

### Requirement: Manager scope includes project teams
A manager's dashboard and team scope SHALL cover the active members of the projects they manage in addition to the members of the departments they manage.

#### Scenario: Project member in scope
- **WHEN** Maya manages a project and Priya is on it but in no department Maya manages
- **THEN** Priya's tasks and workload appear in Maya's dashboard

#### Scenario: Employee outside both
- **WHEN** an employee is neither in Maya's departments nor on her projects
- **THEN** that employee is outside Maya's scope

### Requirement: Project list shows managers
The project list SHALL show each project's managers by name.

#### Scenario: Managers on the card
- **WHEN** a project has managers Maya and Ravi
- **THEN** its card lists both names

#### Scenario: No manager yet
- **WHEN** a project has no managers
- **THEN** its card says there is no manager yet

### Requirement: Existing projects are not migrated
Projects that exist without a flagged manager SHALL keep working, and the manager requirement SHALL apply the next time their team is saved.

#### Scenario: Legacy project
- **WHEN** an existing project has a team but no flagged manager
- **THEN** its team is shown as it is
- **AND** saving that team requires marking a manager

### Requirement: Manager projects are read-only on the employee screen
The Edit employee screen SHALL show the projects a manager oversees as read-only, and there SHALL be no way to set a manager's projects other than the project team dialog.

#### Scenario: Read-only list
- **WHEN** an administrator edits a manager who oversees two projects
- **THEN** the two projects are listed without checkboxes

#### Scenario: Direct assignment is gone
- **WHEN** a client calls the old endpoint that set a manager's projects directly
- **THEN** the endpoint no longer exists
