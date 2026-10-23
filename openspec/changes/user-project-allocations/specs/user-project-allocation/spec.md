## ADDED Requirements

### Requirement: A user can be assigned to many projects with an allocation percentage
A user SHALL be assignable to any number of projects. Each assignment SHALL carry an allocation percentage that is a whole number from 1 to 100. A user SHALL be assigned to a given project at most once. Each assignment SHALL also carry exactly one position on that project.

#### Scenario: Position on each assignment
- **WHEN** an assigner gives a user QA on one project and Developer on another
- **THEN** each assignment keeps its own position

#### Scenario: Two projects
- **WHEN** an assigner gives a user 60% on one project and 40% on another
- **THEN** both assignments are saved and the user shows both projects with their percentages

#### Scenario: Same project twice
- **WHEN** an assigner adds a project the user is already assigned to
- **THEN** the request is refused or merges into a single assignment, never two rows for one project

#### Scenario: Invalid percentage
- **WHEN** the percentage is missing, zero, negative, above 100 or not a whole number
- **THEN** the assignment is refused with a message naming the allowed range

### Requirement: Allocations never total more than 100 percent
The sum of a user's allocation percentages across all their projects SHALL NOT exceed 100. The server SHALL enforce this on every create, update and project-side assignment, regardless of what the screen allowed.

#### Scenario: Exactly 100
- **WHEN** a user's allocations total exactly 100%
- **THEN** the change is accepted

#### Scenario: Over 100
- **WHEN** adding or changing an allocation would make the user's total exceed 100%
- **THEN** the change is rejected, nothing is saved, and the message states the user's current total and what remains

#### Scenario: Over 100 from the project side
- **WHEN** an assigner adds a user to a project with a percentage that would push that user above 100% in total
- **THEN** the request is rejected with the same message and nothing is saved

#### Scenario: Concurrent edits
- **WHEN** two assigners add allocations to the same user at the same time
- **THEN** the cap is still respected: the second request that would exceed 100% is rejected

### Requirement: Assigners see the running total while editing
The employee form SHALL let the assigner add, change and remove project rows (project and percentage), SHALL show the total and the remaining percentage as they edit, and SHALL block saving when the total is above 100.

#### Scenario: Remaining shown
- **WHEN** a user has 70% allocated and the assigner opens the form
- **THEN** the form shows 70% allocated and 30% remaining

#### Scenario: Over-allocation blocked in the form
- **WHEN** the assigner types percentages that total above 100
- **THEN** the total is flagged, the save button is disabled, and no request is sent

### Requirement: Allocations are visible where a user is shown
The employee list, Profile (read-only fields), workload and project member views SHALL show each project with its percentage, and a user's total allocated percentage.

#### Scenario: Profile
- **WHEN** a user opens Profile
- **THEN** their projects are listed with percentages and the total

### Requirement: Existing single projects are migrated
Existing users with a project SHALL become a single 100% allocation to that project with the default position "Member", and users with no project SHALL have no allocations.

#### Scenario: Migration
- **WHEN** the migration runs on data where a user has one project
- **THEN** that user has exactly one allocation of 100% to it with position Member

### Requirement: Removing a project removes its allocations
Deleting a project SHALL remove the allocations that point to it, and affected users' totals SHALL drop accordingly.

#### Scenario: Project deleted
- **WHEN** a project with allocated users is deleted
- **THEN** those allocations are gone and the users' remaining totals are recalculated
