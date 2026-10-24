# Spec Delta

## Purpose

The system SHALL allow tracking worked hours from the Links tab and show remaining/actual hours with full details in Activity History.

## ADDED Requirements

### Requirement: Track hours in Links tab
The system SHALL provide a way to track worked hours from the Links tab of a task.

#### Scenario: Add worked hours
- **WHEN** a user adds worked hours (e.g., today 3h) via the Links tab time tracking
- **THEN** the hours are logged and associated with the task

### Requirement: Show remaining hours
The system SHALL compute and display remaining hours = estimated hours - actual hours, handling null values.

#### Scenario: Remaining hours calculated
- **WHEN** estimated and actual hours are known
- **THEN** remaining hours are displayed as estimated minus actual

### Requirement: Time tracking appears in Activity History
The system SHALL display all time tracking details in the Activity History screen.

#### Scenario: Hours logged appears
- **WHEN** hours are logged via time tracking
- **THEN** a time/hours activity event appears in Activity History with details
