# Spec Delta

## Purpose

Makes profile fields that hold several values easy to read.

## ADDED Requirements

### Requirement: Multi-value fields are bulleted lists
The profile SHALL show Projects, Departments, Managers, Managed projects and Team as a list with one bulleted item per line, never as one comma-separated line.

#### Scenario: Several values
- **WHEN** a user belongs to three departments
- **THEN** the Departments field shows three bulleted lines, one department each

#### Scenario: Managed projects and team
- **WHEN** a manager manages two projects and has five reports
- **THEN** Managed projects shows two bullets and Team shows five bullets

### Requirement: Single and empty values stay clean
A field with exactly one value SHALL show that value on one line, and a field with no values SHALL show a dash.

#### Scenario: One value
- **WHEN** a user has a single manager
- **THEN** the Managers field shows that name on one line with no trailing punctuation

#### Scenario: No value
- **WHEN** a user has no departments
- **THEN** the Departments field shows "—"

### Requirement: Project allocations keep their percentage
Each item in the Projects list SHALL show the project name with its allocated percentage, and the total allocated percentage SHALL remain visible.

#### Scenario: Allocations
- **WHEN** a user is allocated 60% to Atlas and 40% to Beacon
- **THEN** the Projects list shows one bullet per project with its percentage and the 100% total is still shown
