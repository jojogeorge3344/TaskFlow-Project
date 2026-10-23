# Spec Delta

## Purpose
Make the Back button on the task screen return users to the screen they came from instead of always going to the Kanban board.

## ADDED Requirements

### Requirement: Back returns to the originating screen
When a user opens a task from the Task list, a Project detail screen or the Kanban board, the task screen's Back button SHALL return to that screen. The button label SHALL name the destination.

#### Scenario: From Project detail
- **WHEN** a user opens a task from a project's task list and clicks Back
- **THEN** the Project detail screen for that project is shown, labelled "Back to project"

#### Scenario: From the Task list
- **WHEN** a user opens a task from the Task list and clicks Back
- **THEN** the Task list is shown, labelled "Back to tasks"

#### Scenario: From the board
- **WHEN** a user opens a task from the Kanban board and clicks Back
- **THEN** the board is shown, labelled "Back to board"

### Requirement: Return location survives reload and sub-screens
The return location SHALL survive a page reload and SHALL still apply after the user edits, saves, or visits the discussion, activity or reassign sub-screens and returns to the task.

#### Scenario: Reload
- **WHEN** the user reloads the task screen
- **THEN** Back still returns to the originating screen

#### Scenario: After editing
- **WHEN** the user edits and saves the task, then clicks Back
- **THEN** the originating screen is shown

### Requirement: Safe default and validation
When no return location is present, or it is not an in-app path, Back SHALL go to the Task list. An external or malformed location SHALL be ignored.

#### Scenario: Opened directly
- **WHEN** a user opens a task by direct link with no return location
- **THEN** Back goes to the Task list, labelled "Back to tasks"

#### Scenario: External location
- **WHEN** the return location is an absolute URL to another site or a protocol-relative URL
- **THEN** it is ignored and Back goes to the Task list
