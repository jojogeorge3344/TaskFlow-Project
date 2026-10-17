# Spec Delta

## Purpose

Lets Administrators and Managers view and change how automatic deadline reminders behave. This
covers the global switch, the deadline-day reminder, the default intervals for new tasks and the
email preference, all without editing source code.

## ADDED Requirements

### Requirement: Reminder settings access

Only Administrators and Managers SHALL be able to view or change the reminder settings. The
system MUST reject requests from Employees and from unauthenticated callers.

#### Scenario: Manager opens reminder settings

- **WHEN** a Manager requests the reminder settings
- **THEN** the current settings are returned

#### Scenario: Employee is rejected

- **WHEN** an Employee requests or saves the reminder settings
- **THEN** the request is rejected as forbidden and no setting changes

#### Scenario: Unauthenticated request is rejected

- **WHEN** reminder settings are requested without a valid session
- **THEN** the request is rejected as unauthorized

### Requirement: Display the current reminder configuration

The system SHALL return the current reminder configuration. It contains whether reminders are
enabled, whether the deadline-day reminder is enabled, the default first and second intervals in
days, the default overdue frequency, whether email notifications are enabled, and the application
timezone, which is read-only.

#### Scenario: Defaults on a new installation

- **WHEN** reminder settings are requested before anyone has changed them
- **THEN** reminders and the deadline-day reminder are enabled, intervals are 2 and 1 days, overdue frequency is daily, email is disabled, and the configured timezone is shown

### Requirement: Save reminder settings

The system SHALL let an authorized user save the switch, the deadline-day toggle, the default
intervals, the default overdue frequency and the email preference. The new values SHALL take effect
from the next reminder check, with no restart and no code change.

#### Scenario: Settings persist

- **WHEN** a Manager saves intervals of 3 and 1 days and then reloads the settings
- **THEN** the saved values are returned

#### Scenario: Disabling takes effect without restart

- **WHEN** an Administrator disables reminders
- **THEN** reminder checks after the save generate no reminders, without restarting the application

#### Scenario: Timezone cannot be changed here

- **WHEN** a save request includes a different timezone value
- **THEN** the timezone is left unchanged

### Requirement: Validate reminder settings

The system SHALL reject a save whose first interval is outside 1 to 30 days, whose second interval
is outside 0 to 30 days (0 means the deadline day), or whose first interval is not greater than
the second. It SHALL also reject an overdue frequency that is not daily or weekly. A rejected save
MUST return field-level errors and leave the stored settings unchanged.

#### Scenario: Interval out of range

- **WHEN** a save request has a first interval of 45 days
- **THEN** the save is rejected with an error on the first interval and nothing changes

#### Scenario: Second interval not before first

- **WHEN** a save request has a first interval of 1 day and a second interval of 2 days
- **THEN** the save is rejected with an error explaining that the first reminder must come earlier

#### Scenario: Missing field is rejected

- **WHEN** a save request omits any setting, such as whether reminders are enabled
- **THEN** the save is rejected with an error for each missing field and nothing changes

### Requirement: Default intervals apply to new tasks

When a task is created without reminder intervals, the system SHALL use the default intervals
and overdue frequency from the reminder settings. Changing the defaults MUST NOT change the
intervals already stored on existing tasks.

#### Scenario: New task picks up configured defaults

- **WHEN** the defaults are 3 days, 1 day and weekly, and a task is created without reminder intervals
- **THEN** the task is stored with intervals of 3 and 1 days and a weekly overdue frequency

#### Scenario: Mixed explicit and default intervals stay ordered

- **WHEN** the defaults are 3 and 1 days and a task is created with only a second interval of 5 days
- **THEN** the task is rejected because its second reminder would come before its first

#### Scenario: Existing tasks keep their intervals

- **WHEN** the defaults change after a task was created with intervals of 2 and 1 days
- **THEN** that task still has intervals of 2 and 1 days

#### Scenario: Create Task form pre-fills defaults

- **WHEN** a Manager opens the Create Task screen
- **THEN** its reminder fields show the configured default intervals and overdue frequency

### Requirement: Reminder Settings screen

The system SHALL provide a Reminder Settings screen for Administrators and Managers, reachable
from the side navigation. It SHALL show the current configuration and offer the same predefined
interval and frequency choices as Create Task. It SHALL let the user save, and report validation
errors and the save result.

#### Scenario: Screen shows current values

- **WHEN** a Manager opens Reminder Settings
- **THEN** the enable switch, deadline-day toggle, first and second intervals, overdue frequency, email toggle and read-only timezone show the stored values

#### Scenario: Successful save is confirmed

- **WHEN** the user changes a value and saves
- **THEN** the screen confirms the save and shows the stored values

#### Scenario: Invalid input is reported

- **WHEN** the user tries to save a second interval that is not shorter than the first
- **THEN** the screen shows the validation error and the stored settings are not changed

#### Scenario: Employee cannot reach the screen

- **WHEN** an Employee navigates to the Reminder Settings route
- **THEN** access is denied, and no Reminder Settings entry appears in the Employee's navigation

### Requirement: Email preference is recorded only

The system SHALL store and display the email notification preference. Until email delivery is
configured, enabling it MUST NOT stop or delay in-app reminders.

#### Scenario: Email enabled without delivery configured

- **WHEN** email notifications are enabled but no email delivery is configured
- **THEN** in-app reminders are still generated normally
