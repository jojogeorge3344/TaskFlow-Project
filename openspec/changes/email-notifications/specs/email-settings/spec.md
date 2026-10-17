# Spec Delta

## Purpose

Lets an Administrator configure, check and troubleshoot email delivery for deadline reminders from
the Email Configuration screen (Screen 14), without editing source code and without the SMTP
password ever being shown.

## ADDED Requirements

### Requirement: Email configuration access

Only Administrators SHALL be able to view or change the email configuration, send a test email or
view the email delivery log. The system MUST reject Managers, Employees and unauthenticated
callers.

#### Scenario: Administrator opens email configuration

- **WHEN** an Administrator requests the email configuration
- **THEN** the current configuration is returned

#### Scenario: Manager is rejected

- **WHEN** a Manager requests, saves or tests the email configuration, or requests the delivery log
- **THEN** the request is rejected as forbidden and nothing changes

#### Scenario: Unauthenticated request is rejected

- **WHEN** the email configuration is requested without a valid session
- **THEN** the request is rejected as unauthorized

#### Scenario: Navigation entry only for Administrators

- **WHEN** an Administrator, a Manager and an Employee each view the side menu
- **THEN** only the Administrator sees an "Email Settings" entry, and the others are denied if they open its address directly

### Requirement: Display the current email configuration

The system SHALL return the current email configuration. It contains whether email notifications
are enabled, the SMTP server, port, connection security, sender address, sender display name, SMTP
username, the daily send time, whether an SMTP password is configured on the server, and when and
by whom it was last changed. It SHALL NOT include the password itself.

#### Scenario: Defaults on a new installation

- **WHEN** the email configuration is requested before anyone has changed it
- **THEN** email is disabled, the server, sender and username are empty, the port is 587, security is STARTTLS, the send time is 09:00, and the password indicator reflects the server configuration

#### Scenario: Password indicator

- **WHEN** an SMTP password is set in the server configuration and the email configuration is requested
- **THEN** the response says a password is configured and does not contain the password

### Requirement: The email switch is shared with Reminder Settings

The enable/disable email setting on the Email Configuration screen SHALL be the same setting as
the email toggle on the Reminder Settings screen. A change made on either screen SHALL show on the
other.

#### Scenario: Enabled on Email Configuration

- **WHEN** an Administrator enables email on the Email Configuration screen and a Manager then opens Reminder Settings
- **THEN** the email toggle on Reminder Settings shows as enabled

### Requirement: Save email configuration

The system SHALL let an Administrator save the email switch, SMTP server, port, security, sender
address, sender name, username and send time. Changes SHALL take effect from the next email sent,
with no restart. The system SHALL record who made the change and when.

#### Scenario: Configuration persists

- **WHEN** an Administrator saves server "smtp.example.com", port 587 and sender "taskflow@example.com" and reloads the configuration
- **THEN** the saved values are returned along with the time and user of the last change

#### Scenario: Takes effect without restart

- **WHEN** an Administrator changes the SMTP server and the next reminder email is sent
- **THEN** that email uses the new server

### Requirement: Email configuration is validated before saving

The system SHALL reject a save with field-level errors and change nothing when: the port is
outside 1–65535; the send time is not a valid HH:mm time; the sender address or username is too
long, or the sender address is not a valid email address. When email is enabled, the SMTP server
and sender address SHALL be required.

#### Scenario: Invalid port

- **WHEN** an Administrator saves port 70000
- **THEN** the save is rejected with an error on the port field and the stored configuration is unchanged

#### Scenario: Enabling without a server

- **WHEN** an Administrator enables email with an empty SMTP server
- **THEN** the save is rejected with an error on the server field

#### Scenario: Invalid sender address

- **WHEN** an Administrator saves the sender address "not-an-email"
- **THEN** the save is rejected with an error on the sender address field

### Requirement: Configuration problems are reported

The system SHALL report any problem that would stop email from being sent, in plain language,
with the configuration: no SMTP server, no sender address, or a username set while no password is
configured on the server. The Email Configuration screen SHALL show these problems prominently
while email is enabled.

#### Scenario: Password missing for an authenticated server

- **WHEN** a username is saved but no SMTP password is set in the server configuration
- **THEN** the configuration reports that the SMTP password is not configured on the server and explains how to set it

#### Scenario: Complete configuration

- **WHEN** the server, sender, username and server-side password are all present
- **THEN** no configuration problems are reported

### Requirement: Test email

The system SHALL let an Administrator send a test email using the saved configuration, to an
address they enter or, by default, to their own account's email address. The result SHALL say
whether delivery succeeded and, if it failed, give a readable reason. Each test SHALL be recorded
in the delivery log as a test email. A test SHALL work even while email notifications are disabled.

#### Scenario: Successful test

- **WHEN** an Administrator sends a test email with a working configuration
- **THEN** the screen reports success and the delivery log shows a sent test email to that address

#### Scenario: Failed test

- **WHEN** an Administrator sends a test email and the server rejects the credentials
- **THEN** the screen reports that authentication failed, without exposing the password, and the delivery log shows a failed test email

#### Scenario: Invalid test address

- **WHEN** an Administrator requests a test email to "abc"
- **THEN** the request is rejected with a field error and nothing is sent or logged

### Requirement: Recent email deliveries are visible

The system SHALL show Administrators the most recent email deliveries, newest first. Each entry
shows the kind, the recipient address, the subject, the related task when there is one, the
status, the number of attempts, the last error and the relevant times.

#### Scenario: Recent deliveries listed

- **WHEN** an Administrator opens the Email Configuration screen after reminder emails and a test email were processed
- **THEN** the recent deliveries section lists them newest first with their statuses and errors

### Requirement: Email Configuration screen

The Email Configuration screen SHALL show the stored values in a form, read-only password status
with guidance on how to set it, any configuration problems, a save confirmation or field errors,
the test email action and the recent deliveries. It SHALL work at phone width without horizontal
scrolling.

#### Scenario: Form shows stored values

- **WHEN** an Administrator opens Email Settings
- **THEN** the email switch, server, port, security, sender address, sender name, username and send time show the stored values, and the password status shows "Configured" or "Not configured"

#### Scenario: Unsaved changes block testing

- **WHEN** the Administrator has edited the form without saving
- **THEN** the test email action asks them to save first, because tests use the saved configuration
