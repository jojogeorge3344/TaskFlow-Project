## ADDED Requirements

### Requirement: Animated full-screen login background
The login screen SHALL render an animated decorative background that fills the entire viewport behind the sign-in card and legal footer.

#### Scenario: Background covers the viewport
- **WHEN** the login screen is displayed at any viewport size
- **THEN** the animated background covers the full viewport and resizes with the window

#### Scenario: Shown on both login steps
- **WHEN** the user moves from the password step to the two-step verification step
- **THEN** the same background remains displayed behind the card

### Requirement: Background does not affect interaction or accessibility
The background SHALL be purely decorative: hidden from assistive technology, unfocusable, and unable to intercept pointer input.

#### Scenario: Form remains usable
- **WHEN** the user clicks, taps or tabs through the sign-in form while the animation runs
- **THEN** all fields and buttons behave exactly as without the background

#### Scenario: Hidden from assistive technology
- **WHEN** a screen reader reads the login screen
- **THEN** the background is not announced

### Requirement: Reduced-motion support
The system SHALL NOT animate the background when the user's system requests reduced motion, and SHALL show a static background instead.

#### Scenario: Reduced motion requested
- **WHEN** `prefers-reduced-motion: reduce` is set
- **THEN** the login screen shows a static gradient background with no animation loop running

### Requirement: Animation resource hygiene
The animation SHALL pause while the page is hidden and stop completely when the login screen is left.

#### Scenario: Tab hidden
- **WHEN** the browser tab becomes hidden
- **THEN** the animation loop is paused and resumes when the tab is visible again

#### Scenario: Navigating away after sign-in
- **WHEN** the user signs in and the login screen is destroyed
- **THEN** the animation loop and its event listeners are released

## Mockup

Illustrative only (not a requirement). Nodes drift slowly; lines fade in between nearby nodes.

```
+----------------------------------------------------------------------+
|  .        o------o            .                 o                    |
|        .       \                      o-----o  /  \       .          |
|   o-----o       o         .                 \o    o                  |
|         \                       +----------+                         |
|   .      o------o     o         |    TF    |       .      o         |
|                  \    |         | TaskFlow |        \    /          |
|        o          o---o         | Sign in  |         o--o            |
|         \                       | [email ] |                         |
|   .      o-----o      .         | [pass ][Show]                      |
|                  \              | [Sign in]|     o------o            |
|    o---o          o             | Forgot?  |           \             |
|        \                        +----------+     .      o            |
|         o       .       o----o                                       |
|                                                                      |
|           Legal / copyright footer (above the background)           |
+----------------------------------------------------------------------+
 layers (back to front): gradient -> canvas nodes/lines -> card + footer
 reduced motion: gradient only, no nodes or lines
```
