## ADDED Requirements

### Requirement: Theme dropdown in header
The system SHALL show a theme dropdown in the shell header offering **Default**, **Professional** and **Dark**, each with a colour swatch, and mark the active theme.

#### Scenario: Open dropdown
- **WHEN** the user activates the theme control
- **THEN** a list of the three themes is shown with the active one marked selected

#### Scenario: Keyboard use
- **WHEN** the list is open
- **THEN** Arrow Up/Down move between options, Enter or Space selects, and Escape closes it and returns focus to the control

#### Scenario: Close on outside click
- **WHEN** the list is open and the user clicks elsewhere
- **THEN** the list closes without changing the theme

### Requirement: Three themes
The system SHALL provide Default (light surfaces), Professional (navy blue surfaces) and Dark (black surfaces) themes, applied to the whole app, including popups, drawers and the animated backdrop, through `--tf-*` design tokens.

#### Scenario: Select Professional
- **WHEN** the user selects Professional
- **THEN** page, panels, sidebar, forms, popups and backdrop render in the navy palette with readable text

#### Scenario: Select Dark
- **WHEN** the user selects Dark
- **THEN** all screens render with black backgrounds and light text

#### Scenario: Readable text
- **WHEN** any theme is active
- **THEN** body text and interactive controls meet WCAG AA contrast against their background

### Requirement: Default theme
The system SHALL use Default when no theme has been chosen.

#### Scenario: First visit
- **WHEN** a user opens the app with no saved theme
- **THEN** the Default theme is applied

### Requirement: Theme persistence
The system SHALL remember the chosen theme in the browser and apply it before first paint on later visits, falling back to Default if the stored value is missing, invalid or storage is unavailable.

#### Scenario: Reload
- **WHEN** a user selects Dark and reloads the page
- **THEN** Dark is applied immediately with no flash of another theme

#### Scenario: Invalid stored value
- **WHEN** the stored theme value is not one of the three themes
- **THEN** Default is applied

#### Scenario: Storage unavailable
- **WHEN** browser storage throws or is blocked
- **THEN** the app still works with Default and the selection applies for the current session

### Requirement: Login screen excluded
The system SHALL NOT show the theme dropdown on the login screen, which keeps its own appearance.

#### Scenario: Login screen
- **WHEN** the login screen is displayed
- **THEN** no theme dropdown is present
