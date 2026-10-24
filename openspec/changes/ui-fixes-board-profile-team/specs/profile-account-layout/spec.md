# Spec Delta

## Purpose

Arranges the profile page so account facts and editable sections sit in predictable columns.

## ADDED Requirements

### Requirement: Account details on the left
At desktop widths the profile page SHALL show the Account details section in the left column.

#### Scenario: Desktop
- **WHEN** the profile page is shown at 1440px width
- **THEN** Account details is in the left column

### Requirement: Personal information and Password on the right
At desktop widths the profile page SHALL show Personal information and Password, in that order, in the right column.

#### Scenario: Desktop
- **WHEN** the profile page is shown at 1440px width
- **THEN** Personal information sits above Password in the right column

### Requirement: No wasted space under Personal information
The page SHALL NOT leave a large empty region beneath Personal information; the Password section follows it directly.

#### Scenario: Short content
- **WHEN** Personal information has only a few fields
- **THEN** the Password card appears immediately below it, not at the bottom of a tall blank area

### Requirement: Narrow screens stack the sections
Below the tablet breakpoint the sections SHALL stack in one column without horizontal scroll, and every section SHALL stay reachable.

#### Scenario: Phone
- **WHEN** the page is shown at 360px width
- **THEN** all three sections are reachable in one column and the page does not scroll horizontally

### Requirement: Behaviour unchanged
Editing personal details, the two-step status and the change-password link SHALL work as before.

#### Scenario: Save details
- **WHEN** a user edits and saves their personal information
- **THEN** the result is the same as before the layout change
