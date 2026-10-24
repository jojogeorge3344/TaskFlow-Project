# Spec Delta

## Purpose

Defines one consistent, modern look and interaction model for every form, drawer and dialog in TaskFlow, so that fields, sections, pickers, notices and action bars behave the same on every screen.

## ADDED Requirements

### Requirement: Consistent field appearance
Every text input, select, textarea and input group in a form SHALL share one visual style: filled background, rounded border, and visible label above the control.

#### Scenario: Same style on every screen
- **WHEN** a user opens any form screen, drawer or dialog
- **THEN** its inputs, selects and textareas have the same height, radius, border and label style as those on every other form screen

#### Scenario: Attached suffix
- **WHEN** a field has fixed trailing text, such as the email domain
- **THEN** the text appears as a visually attached part of the field and the user types only the part before it

### Requirement: Clear focus and error states
Form controls SHALL show a visible focus indicator, and a control that fails validation SHALL be visually distinct and explain the problem in text.

#### Scenario: Keyboard focus
- **WHEN** a user tabs to a form control
- **THEN** the control shows a highlighted border and focus ring

#### Scenario: Invalid value
- **WHEN** a required field is empty or a value is invalid after the user has interacted or tried to submit
- **THEN** the control shows an error appearance and a message below it states how to fix it
- **AND** the error is not conveyed by colour alone

### Requirement: Sectioned forms
Forms with more than one logical group of fields SHALL present each group as a titled section, with a short hint on the right where useful.

#### Scenario: Add employee sections
- **WHEN** an administrator opens the Add employee panel
- **THEN** the fields appear in four titled sections: personal details, role and team, project allocation, and sign-in and security

#### Scenario: Edit employee sections
- **WHEN** an administrator opens the Edit employee panel
- **THEN** the fields appear in three titled sections: personal details, role and team, and project allocation
- **AND** there is no sign-in and security section

#### Scenario: Required marker
- **WHEN** a field is required
- **THEN** its label shows a required marker and the form explains the marker once

### Requirement: Assignments are managed on their own screens
The Edit employee panel SHALL show an employee's departments and projects as read-only information and SHALL NOT let the user change them. Departments are assigned in Department management and projects in Project management; the panel shows no note saying so for departments. Saving SHALL leave both unchanged.

#### Scenario: Departments are view only
- **WHEN** an administrator edits an employee who belongs to departments
- **THEN** the departments are listed without checkboxes and no instruction text is shown

#### Scenario: Projects are view only
- **WHEN** an administrator edits an employee with project allocations
- **THEN** each project, position and percentage is listed with the capacity bar, there is no add or remove control, and a note points to Project management

#### Scenario: Saving keeps assignments
- **WHEN** an administrator saves an edited employee
- **THEN** the employee's departments and project allocations are sent exactly as loaded, even if their recorded total exceeds 100%

#### Scenario: Manager projects are view only
- **WHEN** an administrator edits an employee who has the Manager role and manages projects
- **THEN** those projects are listed without checkboxes, because they are set by flagging the manager on a project's team

### Requirement: Pill pickers for multiple choice
Choosing several items from a short list, such as the departments of a new employee or the projects a manager oversees, SHALL use selectable pills that show a checked state, and SHALL remain operable by keyboard.

#### Scenario: Select and clear a department
- **WHEN** the user activates a department pill while adding an employee with a click or the space key
- **THEN** the pill toggles between selected and unselected and the underlying choice changes accordingly

#### Scenario: Empty list
- **WHEN** there are no items to choose from
- **THEN** the picker shows a short message instead of an empty area

### Requirement: Identity header on the employee panel
The Edit employee panel SHALL show the employee's initials avatar, full name, email, role, status and employee code above the form.

#### Scenario: Header reflects the record
- **WHEN** an administrator opens Edit employee for an active employee
- **THEN** the header shows that employee's initials, name, email, role, an Active status and the employee code

#### Scenario: Add mode
- **WHEN** the panel is opened to add an employee
- **THEN** the header shows a neutral new-employee state with no stale data

### Requirement: Live project allocation feedback
When adding an employee, the project allocation section SHALL show a capacity bar and a remaining percentage that update as allocations change, and SHALL block saving when the total exceeds 100%.

#### Scenario: Allocation within capacity
- **WHEN** the allocations total 100% or less
- **THEN** the bar segments match each project's percentage and the remaining percentage is shown

#### Scenario: Over-allocation
- **WHEN** the allocations total more than 100%
- **THEN** the bar and a message show the amount over capacity and the Save action is disabled

#### Scenario: Remove a project
- **WHEN** the user removes a project row
- **THEN** its segment disappears and the total and remaining values update

### Requirement: Password entry aids
The temporary-password field on the Add employee panel SHALL let the user reveal or hide the typed value and SHALL show a strength indication, without changing the existing password rules. The Edit employee panel SHALL NOT offer a password field.

#### Scenario: Reveal password
- **WHEN** the user activates the show control
- **THEN** the value becomes readable and the control changes to hide

#### Scenario: Rules unchanged
- **WHEN** the user enters a password shorter than the minimum length
- **THEN** the same validation error as before is shown, regardless of the strength indication

### Requirement: Notices for consequential actions
Warnings that explain a consequence of saving, such as signing the employee out after a username or role change, SHALL appear as a visible callout inside the form, not as small helper text.

#### Scenario: Edit shows sign-out warning
- **WHEN** an administrator edits an existing employee
- **THEN** a warning callout states that changing the username or role signs the employee out

### Requirement: Sticky action bar
Drawers and dialogs with a form SHALL keep their primary and cancel actions visible while the content scrolls, with the primary action on the right and a label that names the outcome.

#### Scenario: Long form
- **WHEN** the form content is taller than the panel
- **THEN** the body scrolls and the header and action bar stay in view

#### Scenario: Saving state
- **WHEN** a save is in progress
- **THEN** the primary action is disabled and its label shows the progress

### Requirement: Responsive stacking
Form layouts SHALL stack to one column on narrow screens without horizontal page scrolling or clipped text.

#### Scenario: Phone width
- **WHEN** a form is shown at about 400px width
- **THEN** two-column field rows become one column, project rows reflow so every control stays usable, and the page does not scroll sideways

### Requirement: Existing behaviour preserved
The refresh SHALL NOT change what the forms submit, their validation rules, role-based access, or the identifiers and attributes that tests and assistive technology rely on.

#### Scenario: Same submission
- **WHEN** a user saves a form after the refresh with the same inputs as before
- **THEN** the same request is sent and the same result is shown

#### Scenario: Accessible names
- **WHEN** a screen reader user visits a refreshed form
- **THEN** every control still has its label, error messages stay linked to their fields, and the dialog or drawer keeps its focus trap and Escape-to-close behaviour
