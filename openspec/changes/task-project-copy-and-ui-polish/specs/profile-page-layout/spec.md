# Spec Delta

## Purpose
Make the profile page use its width well, with a modern look, and stay usable on every screen size.

## ADDED Requirements

### Requirement: Balanced, space-filling layout
The profile page SHALL arrange its sections in a grid that fills the available width, with no large empty regions beside or between sections at common desktop widths (1280px to 1920px).

#### Scenario: Desktop
- **WHEN** the profile page is shown at 1440px width
- **THEN** sections sit in a multi-column grid and no column leaves a visibly empty block next to a taller one

#### Scenario: Sparse content
- **WHEN** a section has little content, such as a user with no project allocations
- **THEN** it shows a compact empty state rather than a tall blank card

### Requirement: Consistent modern styling
The profile page SHALL use the shared card, field and button styles, consistent spacing and a clear visual hierarchy: an identity header first, then details and security sections.

#### Scenario: Visual consistency
- **WHEN** the profile page is compared with other form screens
- **THEN** cards, fields, buttons and spacing follow the same shared styles

### Requirement: Responsive layout
The profile page SHALL collapse to fewer columns as width shrinks, down to a single column at phone width, with no horizontal page scroll.

#### Scenario: Tablet
- **WHEN** the page is shown at 768px width
- **THEN** sections use at most two columns and fill the width

#### Scenario: Phone
- **WHEN** the page is shown at 360px width
- **THEN** sections stack in one column and the page does not scroll horizontally

### Requirement: Behaviour unchanged
Existing profile behaviour, including editing own details, two-step status and the change-password link, SHALL work as before.

#### Scenario: Editing details
- **WHEN** the user edits and saves their details
- **THEN** the result is the same as before the layout change
