# Spec Delta

## Purpose
Keep the assign-team dialog's column headings visually aligned with their inputs at every width.

## ADDED Requirements

### Requirement: Headings align with row columns
The headings Employee, Role, Allocation % and Project manager SHALL sit directly above their columns, sharing the same column widths and spacing as the rows. Heading text SHALL NOT wrap, clip or overflow its column.

#### Scenario: Wide dialog
- **WHEN** the dialog shows one or more team rows on a wide screen
- **THEN** each heading is horizontally aligned with its column's inputs

#### Scenario: Long heading text
- **WHEN** a heading is wider than its column's natural width
- **THEN** the column grows or the heading shortens, and the text is fully readable on one line

### Requirement: Manager control aligns with its heading
The Project manager control in each row SHALL be positioned under the Project manager heading.

#### Scenario: Manager checkbox
- **WHEN** rows are shown
- **THEN** every row's manager checkbox is vertically aligned with the Project manager heading

### Requirement: Responsive without losing labels
On narrow screens the dialog SHALL stack each row's fields and label every field in that row, so no field is shown without a label. It SHALL cause no horizontal scroll.

#### Scenario: Phone width
- **WHEN** the dialog is shown at 400px width
- **THEN** each row stacks its fields, each field shows its own label, and nothing overflows horizontally
