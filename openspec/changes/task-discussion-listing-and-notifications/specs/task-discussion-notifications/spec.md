# Spec Delta

## Purpose

Keeps a task's assigned members informed by in-app notification and Inbox mail whenever its discussion changes.

## ADDED Requirements

### Requirement: Notify members on comment added
When a comment is added to a task's discussion, the system SHALL send an in-app notification and an Inbox mail to each assigned member of the task and to the author.

#### Scenario: Members notified of new comment
- **WHEN** a user adds a comment to a task with an assignee and additional recipients
- **THEN** the assignee, each additional recipient and the author receive a notification and an Inbox mail naming the task and the author

#### Scenario: Author is notified too
- **WHEN** the author is not an assigned member of the task
- **THEN** the author still receives a notification and an Inbox mail for their own action

### Requirement: Notify members on comment edited
When a comment is edited, the system SHALL send an in-app notification and an Inbox mail to each assigned member of the task and to the editor.

#### Scenario: Members notified of edit
- **WHEN** an author edits a comment on a task
- **THEN** each assigned member and the author receive a notification and an Inbox mail saying the comment was edited

### Requirement: Notify members on comment deleted
When a comment is deleted, the system SHALL send an in-app notification and an Inbox mail to each assigned member of the task and to the deleter.

#### Scenario: Members notified of deletion
- **WHEN** an author deletes a comment on a task
- **THEN** each assigned member and the author receive a notification and an Inbox mail saying a comment was deleted

### Requirement: Notification delivery does not lose the change
A failure or disabled preference in notification delivery SHALL NOT discard the comment change, and the in-app notification SHALL respect the global in-app notification preference.

#### Scenario: In-app notifications disabled
- **WHEN** the global in-app notification preference is off and a comment is added
- **THEN** the comment is saved and no in-app notification rows are created
