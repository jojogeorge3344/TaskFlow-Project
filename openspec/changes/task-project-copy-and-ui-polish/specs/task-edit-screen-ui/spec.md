# Spec Delta

## Purpose
Give the task edit screen a modern, sectioned and responsive layout without changing what can be edited or how it is validated.

## ADDED Requirements

### Requirement: Sectioned edit layout
The task edit form SHALL present a header with the task code, title and project, a Details section (title, description, priority), a Schedule section (start, deadline, estimated hours), and an action bar with Save and Cancel.

#### Scenario: Entering edit mode
- **WHEN** a user with permission clicks Edit task
- **THEN** the form shows the header and the Details and Schedule sections, pre-filled with the task's values

#### Scenario: Same fields and rules
- **WHEN** the user edits and saves
- **THEN** the same fields are editable, with the same validation and error messages, as before the redesign

### Requirement: Responsive form
The edit form SHALL use two columns where space allows and a single column on narrow screens. It SHALL cause no horizontal page scroll down to 360px width.

#### Scenario: Desktop
- **WHEN** the form is shown on a wide screen
- **THEN** related fields sit side by side within their section

#### Scenario: Phone width
- **WHEN** the form is shown at 360px width
- **THEN** all fields stack in one column and the page does not scroll horizontally

### Requirement: Reachable actions
Save and Cancel SHALL remain visible without scrolling to the end of a long form, and inline validation errors SHALL appear next to their field.

#### Scenario: Long form on a small screen
- **WHEN** the form is taller than the viewport
- **THEN** the action bar stays visible

#### Scenario: Invalid deadline
- **WHEN** the deadline is not after the start date
- **THEN** an error appears next to the deadline field and Save is blocked
