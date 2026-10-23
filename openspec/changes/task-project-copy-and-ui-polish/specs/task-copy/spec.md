# Spec Delta

## Purpose
Let users create a new task quickly from an existing one by opening a pre-filled Create Task form.

## ADDED Requirements

### Requirement: Copy action on tasks
Users who may create tasks SHALL see a Copy action on each Task list row and on the task screen. Users who may not create tasks SHALL NOT see it.

#### Scenario: Copy from the list
- **WHEN** a user with permission to create tasks clicks Copy on a row
- **THEN** the Create Task screen opens pre-filled from that task

#### Scenario: No permission
- **WHEN** a user without permission to create tasks views the list
- **THEN** no Copy action is shown

### Requirement: Copy pre-fills the Create Task form without saving
The Create Task form opened from a copy SHALL be pre-filled with the source task's title, description, project, assignee, priority, estimated hours, additional recipients and reminder settings. The start date and deadline SHALL reset to the same defaults as a new task. No task SHALL be created until the user submits the form.

#### Scenario: Fields carried over
- **WHEN** the user copies a task
- **THEN** the form shows the source task's title, description, project, assignee, priority, estimated hours, recipients and reminder settings, and the dates show the new-task defaults

#### Scenario: Abandoning a copy
- **WHEN** the user leaves the form without submitting
- **THEN** no new task exists

#### Scenario: Submitting a copy
- **WHEN** the user submits the pre-filled form
- **THEN** a new task with its own code is created and opened, and the source task is unchanged

### Requirement: Copy handles an unavailable source
If the source task cannot be loaded, or its assignee is no longer eligible, the form SHALL still open and tell the user what could not be pre-filled.

#### Scenario: Source not found
- **WHEN** the copy link points to a task that does not exist or the user cannot access
- **THEN** the Create Task form opens empty with a message that the task could not be copied

#### Scenario: Assignee no longer eligible
- **WHEN** the source task's assignee is inactive or no longer an employee
- **THEN** the assignee field is left empty and must be chosen before submitting
