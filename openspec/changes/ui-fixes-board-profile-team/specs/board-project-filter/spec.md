# Spec Delta

## Purpose

Lets people narrow the board to the work of one project.

## ADDED Requirements

### Requirement: Board can be filtered by project
The board SHALL offer a Project filter listing the projects the person can see, with an "All projects" default, and SHALL show only tasks of the chosen project when one is selected.

#### Scenario: Choose a project
- **WHEN** a user selects a project in the Project filter
- **THEN** every column shows only that project's tasks

#### Scenario: Default shows everything
- **WHEN** the board first loads
- **THEN** the Project filter reads "All projects" and no project narrowing is applied

### Requirement: Project filter combines with other filters
The Project filter SHALL combine with the Assignee, Status and Deadline filters using AND semantics, and Clear SHALL reset it with the others.

#### Scenario: Project and status
- **WHEN** a user picks a project and the status "In Progress"
- **THEN** the board shows only that project's In Progress tasks

#### Scenario: Clear
- **WHEN** a user clicks Clear after choosing a project
- **THEN** the Project filter returns to "All projects"

### Requirement: Empty filtered result is explained
When the Project filter leaves no matching tasks, the board SHALL show the existing "no tasks match the current filters" message with a way to clear filters.

#### Scenario: Project without tasks on the board
- **WHEN** a user selects a project that has none of the visible tasks
- **THEN** the board shows the no-match message and a Clear filters action

### Requirement: Cards identify their project
Each board card SHALL show the name of the task's project.

#### Scenario: Card shows project
- **WHEN** a task card is displayed
- **THEN** the project name appears on the card
