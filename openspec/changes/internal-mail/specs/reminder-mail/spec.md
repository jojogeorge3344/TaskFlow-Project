# Spec Delta

## Purpose

Delivers automatic deadline reminders into recipients' TaskFlow Mail Inbox from a "TaskFlow" system
sender, alongside the bell notification, with no external email.

## ADDED Requirements

### Requirement: Reminders are delivered to the Inbox

When delivering reminders to Mail is enabled, every deadline reminder the system generates for a
recipient SHALL also create an Inbox mail for that recipient. The mail SHALL come from the sender
"TaskFlow" and contain the task title, the kind of reminder, the deadline in the application timezone
and a link to the task. The bell notification SHALL still be created as before.

#### Scenario: Reminder arrives as mail

- **WHEN** a second reminder is generated for a task's assignee, creator and an additional recipient
- **THEN** each of them has a new unread Inbox mail from "TaskFlow" about that task, in addition to the bell notification

#### Scenario: Mail delivery disabled

- **WHEN** delivering reminders to Mail is switched off and reminders are generated
- **THEN** only bell notifications are created

### Requirement: One reminder mail per reminder event

The system SHALL create at most one reminder mail per reminder event (task, recipient, reminder type
and day), however often the scheduler runs, including overlapping runs and restarts. The mail SHALL be
written in the same transaction as the reminder history entry and the bell notification, so that
either all of them exist or none of them does.

#### Scenario: Repeated scheduler runs

- **WHEN** the scheduler runs several times on the same day after a reminder was generated
- **THEN** each recipient has exactly one reminder mail for it

### Requirement: System mails cannot be replied to

Reminder mails SHALL behave like other received mails for reading, flags, trash and search. They
SHALL NOT offer Reply or Reply all, because the sender is the system. Forwarding to TaskFlow users
SHALL be allowed.

#### Scenario: Reply refused

- **WHEN** a user tries to reply to a reminder mail
- **THEN** the request is rejected and no mail is sent

### Requirement: Reminder Settings controls mail delivery

The Reminder Settings screen SHALL offer "Also deliver reminders to Mail" in place of the former email
option. It is on by default on new and upgraded installations, and is saved and returned as part of
the reminder settings. Administrators and Managers SHALL be able to change it.

#### Scenario: Default on

- **WHEN** reminder settings are requested on a new installation
- **THEN** delivering reminders to Mail is reported as enabled

#### Scenario: Switched off

- **WHEN** a Manager switches it off and saves
- **THEN** later reminders create bell notifications only
