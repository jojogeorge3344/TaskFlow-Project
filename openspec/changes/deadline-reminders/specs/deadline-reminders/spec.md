# Spec Delta

## Purpose

Automatically reminds a task's recipients as its deadline approaches and after it passes. Each
reminder is generated exactly once per recipient, reminders stop for finished tasks, and the
schedule survives application restarts.

## ADDED Requirements

### Requirement: Reminders are generated automatically in the background

The system SHALL check open tasks for due reminders periodically, without any user action, and
SHALL generate each reminder that is due at the time of the check.

#### Scenario: Reminder appears without user action

- **WHEN** an open task reaches a day on which a reminder is due and the periodic check runs
- **THEN** each of the task's reminder recipients receives an in-app notification for that reminder

#### Scenario: Checking resumes after restart

- **WHEN** the application restarts during a day on which a reminder is due and has not yet been generated
- **THEN** the reminder is generated after the restart, during that same day

### Requirement: Reminder schedule

For an open task the system SHALL generate a first reminder `FirstReminderDays` days before the
deadline day and a second reminder `SecondReminderDays` days before it. On the deadline day it SHALL
generate a due-today reminder, and after the deadline day it SHALL generate overdue reminders at
the task's overdue frequency. Days are calendar days in the application timezone.

#### Scenario: Default schedule

- **WHEN** a task has first and second intervals of 2 and 1 days and its deadline is on 15 October
- **THEN** reminders are generated on 13 October (first), 14 October (second) and 15 October (due today)

#### Scenario: Daily overdue reminders

- **WHEN** a task with a daily overdue frequency remains open after its deadline day of 15 October
- **THEN** one overdue reminder is generated on each of 16, 17 and 18 October, continuing daily while it stays open

#### Scenario: Weekly overdue reminders

- **WHEN** a task with a weekly overdue frequency remains open after its deadline day of 15 October
- **THEN** overdue reminders are generated on 22 October, 29 October and every seven days thereafter while it stays open

#### Scenario: Interval of zero means the deadline day

- **WHEN** a task's first or second reminder interval is 0 ("on the deadline day")
- **THEN** recipients get a single due-today reminder on the deadline day and no separate first or second reminder

#### Scenario: Equal intervals produce one reminder

- **WHEN** a task's first and second reminder intervals are equal and non-zero
- **THEN** only one pre-deadline reminder is generated for each recipient on that day

#### Scenario: Due-today reminder only before the deadline passes

- **WHEN** the check runs on the deadline day after the deadline time has already passed
- **THEN** no due-today reminder is generated, and the task waits for its overdue reminders

### Requirement: Reminder recipients

Each reminder SHALL be addressed to every current notification recipient of the task. These are
the assigned employee and any additional recipients, plus the task's creator. Each person
receives it once, even when they hold several of these roles. Inactive or deleted users SHALL NOT
receive reminders.

#### Scenario: Assignee, creator and additional recipient are reminded

- **WHEN** a reminder is generated for a task with an assignee, a creator and one additional recipient
- **THEN** each of the three users receives exactly one notification for that reminder

#### Scenario: Creator who is also a recipient is reminded once

- **WHEN** the task's creator is also one of its additional recipients
- **THEN** the creator receives a single notification for that reminder

#### Scenario: Inactive recipient is skipped

- **WHEN** one of the task's recipients has been deactivated or deleted
- **THEN** that user receives no reminder, and the other recipients still do

### Requirement: Reminders stop for finished tasks

The system MUST NOT generate any further reminder for a task that is completed or cancelled, or
for a task that has been deleted.

#### Scenario: Completed task stops reminding

- **WHEN** a task is marked completed before its second reminder day
- **THEN** no second, due-today or overdue reminder is generated for it

#### Scenario: Cancelled task stops reminding

- **WHEN** an overdue task is cancelled
- **THEN** no further overdue reminders are generated for it

### Requirement: Past reminder slots are not backfilled

The system MUST NOT generate a reminder for a day that has already ended in the application
timezone, including a day missed while the application was not running.

#### Scenario: Task created after its first reminder day

- **WHEN** a task is created one day before its deadline with a first reminder interval of 2 days
- **THEN** no first reminder is generated, and the second and due-today reminders follow normally

#### Scenario: Downtime spanning a reminder day

- **WHEN** the application was not running for the whole of a task's second reminder day
- **THEN** that second reminder is never generated after the application starts again

### Requirement: Deadline changes recalculate future reminders

The system SHALL base every reminder on the task's current deadline, so changing the deadline
moves all reminders that have not yet been generated to the new schedule.

#### Scenario: Deadline extended

- **WHEN** a task's deadline is moved from 15 October to 20 October before any reminder is generated
- **THEN** reminders follow the 20 October schedule and none is generated for the 15 October schedule

#### Scenario: Overdue task given a new future deadline

- **WHEN** an overdue task's deadline is changed to a future date
- **THEN** overdue reminders stop and the task's pre-deadline reminders follow the new deadline

### Requirement: Duplicate reminders are prevented

The system SHALL record every generated reminder in a reminder history that holds the task, the
recipient, the reminder type, the scheduled slot, the processing status and the time it was
processed. A reminder with the same task, recipient, type and slot MUST NOT be generated twice,
however many times the check runs.

#### Scenario: Repeated checks on the same day

- **WHEN** the periodic check runs many times during a day on which a reminder is due
- **THEN** each recipient receives exactly one notification for that reminder

#### Scenario: Concurrent or restarted checks

- **WHEN** two checks process the same due reminder, or the application restarts after generating it
- **THEN** only one history entry and one notification per recipient exist for that reminder

#### Scenario: History records the reminder

- **WHEN** a reminder is generated
- **THEN** a history entry exists with the task, recipient, reminder type, scheduled slot, a sent status and the processing time

### Requirement: Consistent application timezone

The system SHALL determine reminder days by converting deadlines and the current time to a
single configured application timezone. All timestamps are still stored in UTC.

#### Scenario: Deadline near midnight

- **WHEN** the application timezone is UTC+05:30 and a task's deadline is 20:00 UTC on 14 October (01:30 local on 15 October)
- **THEN** the task's deadline day for reminders is 15 October

### Requirement: Global reminder switch

When reminders are disabled in the reminder settings, the system MUST NOT generate any
reminder. When the deadline-day reminder is disabled, it MUST NOT generate due-today reminders
unless one of the task's own intervals is 0, which explicitly asks for a reminder on the deadline day.

#### Scenario: Reminders disabled

- **WHEN** reminders are disabled and a task reaches a reminder day
- **THEN** no reminder is generated for it

#### Scenario: Re-enabled later the same day

- **WHEN** reminders are re-enabled during a day on which a reminder is due
- **THEN** that day's reminder is generated, and reminders for days that ended while disabled are not

#### Scenario: Deadline-day reminder disabled

- **WHEN** the deadline-day reminder is disabled and a task reaches its deadline day
- **THEN** no due-today reminder is generated, while first, second and overdue reminders continue

#### Scenario: Task asks for a deadline-day reminder while the global toggle is off

- **WHEN** the deadline-day reminder is disabled and a task with a second interval of 0 reaches its deadline day
- **THEN** that task's recipients still receive one due-today reminder

### Requirement: Reminder notifications identify their task and kind

Each reminder notification SHALL refer to its task, so the Notification Center can open it. It
SHALL carry a deadline-reminder type for first, second and due-today reminders and an overdue
type for overdue reminders. The message SHALL state the task title and when it is due.

#### Scenario: Due-today notification

- **WHEN** a due-today reminder is generated for "Prepare Monthly Report"
- **THEN** the recipient's notification has the deadline-reminder type, links to that task, and its message names the task and says it is due today

#### Scenario: Overdue notification

- **WHEN** an overdue reminder is generated
- **THEN** the notification has the overdue type and links to the task
