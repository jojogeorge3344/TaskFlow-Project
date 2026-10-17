# Spec Delta

## Purpose

Lets each signed-in user review, count, filter and clear the in-app notifications addressed to
them, and open the task a notification refers to, without ever seeing another user's
notifications.

## ADDED Requirements

### Requirement: Notifications are scoped to the signed-in user

The system SHALL return only notifications addressed to the authenticated user for listing, the
unread count and mark-as-read, and MUST reject any attempt to read or change another user's
notifications.

#### Scenario: List contains only the caller's notifications

- **WHEN** an authenticated user requests their notifications
- **THEN** every returned entry belongs to that user and none addresses another user

#### Scenario: Unread count excludes other users

- **WHEN** the unread count is requested
- **THEN** it counts only the authenticated user's unread notifications

#### Scenario: Marking another user's notification is rejected

- **WHEN** a user attempts to mark a notification that is not addressed to them
- **THEN** the system rejects the request and leaves that notification unchanged

#### Scenario: Unauthenticated request is rejected

- **WHEN** a request for notifications is made without a valid session
- **THEN** the system rejects it as unauthorized

### Requirement: Notification list

The system SHALL present the user's notifications as a paged list, newest first, returning the
items together with the total count and page metadata.

#### Scenario: Paged results

- **WHEN** the user requests a page of notifications
- **THEN** the response contains that page of items, the total count and the page metadata

#### Scenario: Newest first

- **WHEN** a user has several notifications
- **THEN** the most recently created notification appears before older ones

### Requirement: Notification entry details

Each notification entry SHALL expose its type, message, read state and creation timestamp, and,
when it relates to a task, the related task so the user can open it.

#### Scenario: Task-related entry exposes its task

- **WHEN** a notification was created for a task assignment or reassignment
- **THEN** the entry identifies the related task as well as its type, message, read state and timestamp

#### Scenario: Entry without a related task

- **WHEN** a notification is not related to any task
- **THEN** the entry is shown without a task link and is otherwise complete

### Requirement: Unread count

The system SHALL expose the number of the authenticated user's unread notifications so the
interface can show a badge.

#### Scenario: Count reflects unread state

- **WHEN** the user has a mix of read and unread notifications
- **THEN** the unread count equals the number of unread ones only

#### Scenario: Count updates after reading

- **WHEN** the user marks a notification as read
- **THEN** the unread count decreases accordingly

### Requirement: Filtering notifications

The system SHALL allow the user to filter their notifications by notification type and by read
state.

#### Scenario: Filter applied

- **WHEN** the user applies a type or read-state filter
- **THEN** only the notifications matching the selected filter are shown

### Requirement: Mark a notification as read

The system SHALL allow the user to mark one of their own notifications as read, and the change
SHALL persist.

#### Scenario: Marking as read persists

- **WHEN** the user marks one of their unread notifications as read
- **THEN** that notification is reported as read on subsequent reads

#### Scenario: Marking an already-read notification is harmless

- **WHEN** the user marks a notification that is already read
- **THEN** the notification remains read and no error occurs

### Requirement: Mark all notifications as read

The system SHALL allow the user to mark all of their notifications as read in a single action.

#### Scenario: All marked read

- **WHEN** the user chooses to mark all notifications as read
- **THEN** every one of their notifications becomes read and the unread count becomes zero

### Requirement: Open the related task

The system SHALL let the user open the task a notification refers to.

#### Scenario: Opening a task notification

- **WHEN** the user opens a notification that relates to a task
- **THEN** the interface takes the user to that task's details
