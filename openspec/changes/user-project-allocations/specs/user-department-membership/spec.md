## ADDED Requirements

### Requirement: A user can belong to many departments
A user SHALL be able to belong to any number of departments, with no percentage. Membership is managed from the employee form and from the Department members screen.

#### Scenario: Two departments
- **WHEN** an assigner adds a user to two departments
- **THEN** the user appears in the member list of both departments

#### Scenario: Remove from one department
- **WHEN** a user is removed from one department
- **THEN** they remain in their other departments

#### Scenario: Duplicate membership
- **WHEN** the same department is added twice for a user
- **THEN** only one membership exists

### Requirement: A user's managers are derived from their departments
A user's managers SHALL be the managers of the departments the user belongs to. The system SHALL NOT store a separate manager for a user. A user SHALL NOT be listed as their own manager.

#### Scenario: Several managers
- **WHEN** a user belongs to two departments that have different managers
- **THEN** the user shows both managers, each once

#### Scenario: Manager change follows departments
- **WHEN** a department's manager changes
- **THEN** the managers shown for that department's members change with it

#### Scenario: No department
- **WHEN** a user belongs to no department
- **THEN** they have no managers

### Requirement: Existing single department and manager are migrated
Existing users with a department SHALL become a member of that department. Existing single manager values SHALL stop being used, and managers SHALL come from department membership from then on.

#### Scenario: Migration
- **WHEN** the migration runs on a user who had one department
- **THEN** the user is a member of that department

### Requirement: Filters match any membership
Filtering by department or project in lists, workload, reports, task filters and dashboards SHALL include a user when any of their memberships or allocations match.

#### Scenario: Department filter
- **WHEN** a manager's dashboard is filtered to one department
- **THEN** users who belong to that department and others are included once

### Requirement: Deleting a department removes its memberships
Deleting a department SHALL remove its membership rows and its manager links, and users' manager lists SHALL update accordingly.

#### Scenario: Department deleted
- **WHEN** a department is deleted
- **THEN** its members no longer list it or its managers through it
