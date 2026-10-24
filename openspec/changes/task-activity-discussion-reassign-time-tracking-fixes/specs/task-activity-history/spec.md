# Spec Delta

## Purpose

The system SHALL correctly filter and display all task activity events including reassignments and time tracking events.

## ADDED Requirements

### Requirement: Activity History filters work
The system SHALL apply event type, date, user, and search filters on the Activity History screen including filters passed via URL query parameters.

#### Scenario: Filter by event type from query params
- **WHEN** navigating to Activity History with eventType query parameter
- **THEN** the activity list is filtered to that event type

#### Scenario: Filters persist in UI
- **WHEN** filters are applied
- **THEN** the filtered results are shown and filter controls reflect applied values

### Requirement: Reassign events visible in Activity History
The system SHALL display task reassignment events in Activity History.

#### Scenario: Task reassigned
- **WHEN** a task is reassigned
- **THEN** a Reassigned activity event appears in Activity History with old/new assignee details
