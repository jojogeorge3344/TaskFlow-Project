# Spec Delta

## Purpose

Keeps the project team dialog free of redundant labels while still letting an administrator flag project managers.

## ADDED Requirements

### Requirement: No repeated Manager label on team rows
A team row SHALL show its manager checkbox without a visible "Manager" text beside it, because the column heading already reads "Project manager".

#### Scenario: Row display
- **WHEN** the project team dialog lists team members
- **THEN** each row shows a manager checkbox under the "Project manager" heading and no extra "Manager" word

### Requirement: Manager control stays usable and accessible
The manager checkbox SHALL keep its behaviour: it is disabled with an explanation for people without the Manager role, and it keeps an accessible name identifying the person's row.

#### Scenario: Non-manager role
- **WHEN** a team member does not hold the Manager role
- **THEN** their checkbox is disabled and its tooltip explains that only a person with the Manager role can manage a project

#### Scenario: Screen reader name
- **WHEN** assistive technology reads a row's checkbox
- **THEN** it announces it as the project manager control for that employee
