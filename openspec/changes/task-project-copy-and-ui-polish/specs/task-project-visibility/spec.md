# Spec Delta

## Purpose
Make a task's project visible wherever tasks are listed or opened, and let users narrow the Task list to one project.

## ADDED Requirements

### Requirement: Task list shows the project of each task
The Task list SHALL show a Project column with each task's project name, or "—" when the task has no project.

#### Scenario: Task with a project
- **WHEN** the Task list loads a task that belongs to project "Apollo"
- **THEN** that row shows "Apollo" in the Project column

#### Scenario: Legacy task without a project
- **WHEN** the Task list loads a task with no project
- **THEN** that row shows "—" in the Project column

### Requirement: Task list can be filtered by project
The Task list SHALL offer a Project filter listing the projects the user can see, with an "All projects" default. Choosing a project SHALL return only that project's tasks, return to page 1, and combine with the other filters. Clearing filters SHALL reset it.

#### Scenario: Filter by project
- **WHEN** the user picks project "Apollo" in the Project filter
- **THEN** only tasks of "Apollo" are listed, the count reflects the filtered total, and the list is on page 1

#### Scenario: Combined with another filter
- **WHEN** a project and a status are both selected
- **THEN** only tasks matching both are listed

#### Scenario: Reset filters
- **WHEN** the user resets the filters
- **THEN** the Project filter returns to "All projects"

### Requirement: Task screen shows the project read-only
The task screen SHALL show the task's project name in its header and in the edit form. The project SHALL NOT be changeable from the edit form.

#### Scenario: Viewing a task
- **WHEN** a user opens a task that belongs to a project
- **THEN** the project name is shown with the task code and title

#### Scenario: Editing a task
- **WHEN** a user edits a task
- **THEN** the project appears as a non-editable value, and saving does not change the task's project

#### Scenario: Legacy task without a project
- **WHEN** a user opens a task with no project
- **THEN** the project shows "—" and the task stays editable
