## ADDED Requirements

### Requirement: Task creation requires a project
The system SHALL refuse to create a task unless it is mapped to an existing, non-deleted project.

#### Scenario: Create without a project
- **WHEN** a task is submitted with no project
- **THEN** the request is rejected with a validation error saying a project is required and no task is created

#### Scenario: Create with an unknown project
- **WHEN** a task is submitted with a project id that does not exist or was deleted
- **THEN** the request is rejected and no task is created

#### Scenario: Create with a valid project
- **WHEN** a task is submitted with an existing project and all other fields valid
- **THEN** the task is created and mapped to that project

### Requirement: Create Task form has a mandatory Project field
The Create Task screen SHALL show a required Project selector listing the available projects and SHALL block submission until one is chosen.

#### Scenario: Submit with no project selected
- **WHEN** the user submits the form without choosing a project
- **THEN** a "Project is required." message is shown and no request is sent

### Requirement: Existing projectless tasks are unaffected
Tasks created before this change that have no project SHALL remain viewable and editable.

#### Scenario: Edit a legacy task
- **WHEN** a task without a project is updated or its status changed
- **THEN** the update succeeds without requiring a project
