# Spec Delta

## Purpose

Delivers deadline reminders by email over SMTP alongside the in-app notifications. Each email is
sent at a sensible time of day, retried a limited number of times and recorded in a delivery log.
In-app notifications keep working whenever email fails.

## ADDED Requirements

### Requirement: Reminders are emailed when email is enabled

When email notifications are enabled, the system SHALL send an email for every deadline reminder
it generates for a recipient who has an email address. The email SHALL go to that recipient's
address and contain the same information as the in-app notification: the task title, the kind of
reminder and the deadline.

#### Scenario: Reminder emailed to each recipient

- **WHEN** email is enabled and a two-days-before reminder is generated for a task's assignee, creator and one additional recipient
- **THEN** each of the three recipients receives one email about that task and reminder, in addition to their in-app notification

#### Scenario: Email disabled

- **WHEN** email notifications are disabled and reminders are generated
- **THEN** only in-app notifications are created and no email is queued or sent

#### Scenario: Recipient without an email address

- **WHEN** a reminder is generated for a recipient whose account has no email address
- **THEN** that recipient receives the in-app notification and no email is queued for them

### Requirement: One email per reminder event

The system SHALL queue at most one email for each generated reminder event (task, recipient,
reminder type and day), however many times the scheduler runs. This SHALL hold when scheduler runs
overlap or the application restarts.

#### Scenario: Repeated scheduler runs

- **WHEN** the scheduler runs several times on the same day after a reminder was generated
- **THEN** exactly one email exists for that reminder and recipient

### Requirement: Emails wait for the configured send time

The system SHALL NOT send a reminder email before the configured send time (default 09:00) on
that reminder's day, in the application timezone. A reminder generated after the send time SHALL
be emailed on the next scheduler run. The in-app notification SHALL appear as soon as the reminder
is generated, whatever the send time.

#### Scenario: Reminder generated at midnight

- **WHEN** a reminder is generated at 00:01 local time and the send time is 09:00
- **THEN** the in-app notification appears at 00:01 and the email is sent on the first scheduler run at or after 09:00

#### Scenario: Reminder generated after the send time

- **WHEN** a task created at 14:00 local time produces a due-today reminder and the send time is 09:00
- **THEN** the email is sent on the next scheduler run

### Requirement: In-app notifications are independent of email

Email delivery SHALL NOT prevent, delay or roll back in-app notifications. A failed send, an
unreachable SMTP server, invalid credentials or incomplete email settings MUST leave the reminder's
in-app notification and its reminder history unchanged.

#### Scenario: SMTP server unreachable

- **WHEN** email is enabled, the SMTP server cannot be reached and a reminder is generated
- **THEN** every recipient still receives the in-app notification, and the email delivery is recorded as failed once its retries are used up

#### Scenario: Email settings incomplete

- **WHEN** email is enabled but no SMTP server is configured and a reminder is generated
- **THEN** the in-app notification is created and the email attempt is recorded with a configuration error

### Requirement: Failed emails are retried a limited number of times

The system SHALL retry a failed reminder email on later scheduler runs, waiting longer after each
failure, up to 3 attempts in total. After the third failed attempt the email SHALL be marked failed
and never sent again.

#### Scenario: Temporary failure then success

- **WHEN** the first attempt fails because the server is temporarily unavailable and a later attempt succeeds
- **THEN** the email is marked sent and the log shows 2 attempts

#### Scenario: Permanent failure

- **WHEN** all 3 attempts to send an email fail
- **THEN** the email is marked failed with the last error, and no further attempt is made

### Requirement: Stale reminder emails are not sent

The system SHALL NOT send a reminder email after the end of its reminder day in the application
timezone. If the reminder's task has been completed, cancelled, deleted or rescheduled, or email
has been disabled before the email is sent, the email SHALL be marked skipped with a reason instead
of being sent.

#### Scenario: Application down all day

- **WHEN** an email is queued, the application is stopped before the send time and started again the next day
- **THEN** that email is marked skipped and not sent

#### Scenario: Task completed before the send time

- **WHEN** a reminder email is waiting for the send time and the task is completed before then
- **THEN** the email is marked skipped and not sent

#### Scenario: Email disabled before sending

- **WHEN** reminder emails are queued and an Administrator disables email before they are sent
- **THEN** those emails are marked skipped and not sent

### Requirement: Email delivery is logged

The system SHALL keep a persistent record of each email: the kind (reminder or test), the
recipient address, the subject, the related task and recipient for reminder emails, the status
(pending, sent, failed or skipped), the number of attempts, the last error, and when it was
created, last attempted and sent. Each attempt for a reminder email SHALL also be counted on that
reminder's history entry.

#### Scenario: Successful delivery recorded

- **WHEN** a reminder email is sent on the first attempt
- **THEN** its log entry shows status sent, 1 attempt and the sent time, and the reminder history entry shows 1 delivery attempt

#### Scenario: Failure recorded

- **WHEN** an attempt fails because authentication is rejected
- **THEN** the log entry records the attempt and a readable error describing the authentication failure

#### Scenario: Log survives restart

- **WHEN** the application is restarted
- **THEN** all previously recorded email deliveries and their statuses are still present

### Requirement: SMTP credentials are never exposed

The system SHALL read the SMTP password only from server configuration, such as environment
variables or user-secrets. It MUST NOT store the password in the database, return it from any API,
display it, or write it to logs, error messages or the delivery log.

#### Scenario: Password absent from responses and logs

- **WHEN** an Administrator retrieves the email settings, sends a test email that fails authentication, and reviews the delivery log
- **THEN** no response, log entry or recorded error contains the SMTP password

#### Scenario: Password absent from the database

- **WHEN** the email settings are saved
- **THEN** no database table contains the SMTP password
