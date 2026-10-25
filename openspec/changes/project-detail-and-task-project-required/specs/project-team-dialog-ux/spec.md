## ADDED Requirements

### Requirement: Column headings above the team rows
The assign-team dialog SHALL show a heading row above the member rows labelled Employee, Role, Allocation % and Project manager, aligned with the row columns, shown only when at least one row exists.

#### Scenario: Rows present
- **WHEN** the dialog has one or more rows
- **THEN** the headings are visible above the first row

#### Scenario: No rows
- **WHEN** the team is empty
- **THEN** no heading row is shown

### Requirement: New rows have no default role
Adding a member row SHALL leave the role field empty; the system MUST NOT pre-fill "Member". The empty field SHALL show the placeholder "Role in this project".

#### Scenario: Add a row
- **WHEN** the user clicks Add employee
- **THEN** the role field is empty and shows the placeholder "Role in this project"

#### Scenario: Save with empty role
- **WHEN** the user saves while a row's role is empty
- **THEN** the row is flagged and the team is not saved
