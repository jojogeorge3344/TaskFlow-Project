## ADDED Requirements

### Requirement: App-wide animated backdrop
The system SHALL render a decorative animated backdrop behind every authenticated screen, visible in the area outside the screen content (page margins and gutters), without covering or reducing the legibility of content.

#### Scenario: Backdrop shown on a screen
- **WHEN** a user opens any authenticated screen
- **THEN** an animated backdrop is rendered behind the shell and page content
- **AND** it does not receive pointer events or keyboard focus

#### Scenario: Backdrop persists across navigation
- **WHEN** a user navigates between screens or tabs
- **THEN** the same backdrop continues without restarting or flickering

### Requirement: Backdrop behind popups
The system SHALL keep the animated backdrop visible behind popups, modals and drawers, with the existing dimmed overlay on top.

#### Scenario: Popup open
- **WHEN** a user opens a modal or drawer
- **THEN** the animated backdrop is still visible behind the dimmed overlay
- **AND** the popup content remains fully legible

### Requirement: Backdrop follows the active theme
The system SHALL colour the backdrop (gradient base and nodes) from the active theme.

#### Scenario: Theme changes
- **WHEN** the user selects a different theme
- **THEN** the backdrop colours update immediately without a reload

### Requirement: Reduced motion and accessibility
The system SHALL NOT animate the backdrop when the user prefers reduced motion, and SHALL hide it from assistive technology.

#### Scenario: Reduced motion preferred
- **WHEN** the operating system reports `prefers-reduced-motion: reduce`
- **THEN** only a static gradient is shown and no animation frames run

#### Scenario: Assistive technology
- **WHEN** a screen reader traverses the page
- **THEN** the backdrop is not announced (`aria-hidden="true"`)

### Requirement: Performance safeguards
The system SHALL cap node count and pixel ratio, pause animation while the tab is hidden, and release all listeners and frames when the backdrop is destroyed.

#### Scenario: Tab hidden
- **WHEN** the browser tab becomes hidden
- **THEN** the animation loop stops, and resumes when the tab is visible again

### Requirement: Login screen unchanged
The system SHALL keep the existing login background; the app-wide backdrop MUST NOT render on the login screen.

#### Scenario: Login screen
- **WHEN** an unauthenticated user views the login screen
- **THEN** only the login animated background is rendered
