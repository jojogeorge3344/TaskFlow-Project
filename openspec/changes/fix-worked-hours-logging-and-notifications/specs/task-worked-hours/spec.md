# Spec Delta

## Purpose

The system SHALL let users log, correct and clear a task's worked hours, reflect them immediately on screen, and tell the task's assigned members about every change.

## ADDED Requirements

### Requirement: Log hours without leaving the screen
The system SHALL add the entered hours to the task's worked total without a page reload or navigation when the user clicks Log hours.

#### Scenario: Hours logged
- **WHEN** a user enters 3 and clicks Log hours on a task with 2 worked hours
- **THEN** the screen stays in place and Worked shows 5h and Remaining is recomputed
- **AND** the input is cleared and a success toast is shown

#### Scenario: Invalid hours
- **WHEN** the entered value is empty, zero or negative
- **THEN** nothing is sent and an error toast asks for hours greater than 0

### Requirement: Worked hours field reflects logged time
The system SHALL show the current worked total in the Worked hours field of the edit form, including after hours are logged while the form is open.

#### Scenario: Log while editing
- **WHEN** the edit form is open and the user logs 1 hour
- **THEN** the Worked hours field shows the new total

### Requirement: Correct or clear worked hours
The system SHALL let a user with edit permission set the worked total to any value of 0 or more, where 0 clears it.

#### Scenario: Clear
- **WHEN** a user empties the Worked hours field and saves
- **THEN** the task's worked hours become 0 and an activity entry records the change

### Requirement: Notify assigned members of hour changes
The system SHALL, whenever worked hours are added, updated or cleared, publish an in-app notification and send an internal mail to the task's assignee and recipients, including the user who made the change.

#### Scenario: Hours added
- **WHEN** a user logs hours on a task
- **THEN** every assigned member, including the user who logged the hours, gets a notification and a mail stating who changed the hours, the old and new totals and the task title

#### Scenario: Hours cleared
- **WHEN** worked hours are cleared
- **THEN** the same members are notified that the hours were removed

#### Scenario: No change
- **WHEN** an update leaves the worked hours unchanged
- **THEN** no notification or mail is sent

#### Scenario: Mail failure
- **WHEN** sending a mail to a member fails
- **THEN** the hours change is still saved
