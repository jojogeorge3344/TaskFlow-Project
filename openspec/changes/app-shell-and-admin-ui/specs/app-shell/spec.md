# Spec Delta

## Purpose

The application shell is the shared frame around every authenticated TaskFlow screen. It provides a header bar, role-based side navigation with icons, a collapsible menu, and the account and sign-out area.

## ADDED Requirements

### Requirement: Shared shell on every authenticated screen

The system SHALL render every authenticated screen inside the same application shell: a header bar, a side navigation menu, and a main content area. The login screen SHALL NOT render the shell.

#### Scenario: Management screen shows the shell

- **WHEN** an Administrator opens `/employees` or `/departments`
- **THEN** the header bar and side navigation menu are shown around the screen content

#### Scenario: Dashboards show the same shell

- **WHEN** a user opens their role dashboard (`/admin`, `/manager`, or `/me`)
- **THEN** the same header bar and side navigation menu are shown

#### Scenario: Login has no shell

- **WHEN** an unauthenticated user opens `/login`
- **THEN** no header bar or side navigation menu is shown

### Requirement: Role-based navigation items with icons

The side navigation SHALL show only the items allowed for the signed-in user's role. Each item SHALL have an icon and a text label. Items for screens that are not built yet SHALL show as disabled with a "Soon" indicator and SHALL NOT navigate.

#### Scenario: Administrator menu

- **WHEN** an Administrator is signed in
- **THEN** the menu shows Dashboard, Employee Management, and Department Management as links, and Task Management as disabled "Soon"

#### Scenario: Employee menu

- **WHEN** an Employee is signed in
- **THEN** the menu shows Dashboard as a link and My Tasks as disabled "Soon", and shows no administration items

#### Scenario: Disabled item does nothing

- **WHEN** a user clicks a disabled "Soon" item
- **THEN** the current route does not change

### Requirement: Active navigation item is highlighted

The side navigation SHALL visually highlight the item that matches the current route and SHALL mark it with `aria-current="page"`.

#### Scenario: Highlight on the employees screen

- **WHEN** an Administrator is on `/employees`
- **THEN** the Employee Management item is highlighted and has `aria-current="page"`

### Requirement: Collapse and expand the side menu on wide screens

On viewports at least 992px wide, a toggle icon button in the header bar SHALL switch the side menu between expanded (icons and labels) and collapsed (icons only). The main content area SHALL resize to fill the freed space.

#### Scenario: Collapse the menu

- **WHEN** the menu is expanded and the user clicks the toggle button
- **THEN** the menu shrinks to an icon-only rail, the labels are hidden, and the content area widens

#### Scenario: Expand the menu

- **WHEN** the menu is collapsed and the user clicks the toggle button
- **THEN** the menu shows icons and labels again

#### Scenario: Label available while collapsed

- **WHEN** the menu is collapsed and the user hovers over or focuses an item
- **THEN** the item's label is shown as a tooltip and is still exposed as the item's accessible name

### Requirement: Remember the collapsed state

The system SHALL remember the wide-screen collapsed or expanded state in the current browser and restore it on reload and across screens. If the stored value cannot be read, the menu SHALL default to expanded.

#### Scenario: State survives reload

- **WHEN** the user collapses the menu and reloads the page
- **THEN** the menu is still collapsed

#### Scenario: Storage unavailable

- **WHEN** browser storage is blocked or empty
- **THEN** the menu starts expanded and the toggle still works

### Requirement: Off-canvas drawer on narrow screens

On viewports narrower than 992px, the side menu SHALL be hidden by default and the toggle button SHALL open it as an overlay drawer with a backdrop. The drawer SHALL close when the user picks a link, clicks the backdrop, or presses Escape.

#### Scenario: Open the drawer on mobile

- **WHEN** a user on a 390px-wide screen taps the toggle button
- **THEN** the menu slides in over the content with a dimmed backdrop, showing icons and labels

#### Scenario: Close by navigating

- **WHEN** the drawer is open and the user taps a navigation link
- **THEN** the app navigates and the drawer closes

#### Scenario: Close by backdrop or Escape

- **WHEN** the drawer is open and the user taps the backdrop or presses Escape
- **THEN** the drawer closes and focus returns to the toggle button

### Requirement: Accessible toggle control

The toggle button SHALL have an accessible name, and SHALL use `aria-expanded` to report whether the menu is currently expanded (wide screens) or open (narrow screens). It SHALL be operable with the keyboard.

#### Scenario: Screen reader state

- **WHEN** the menu is collapsed on a wide screen
- **THEN** the toggle button reports `aria-expanded="false"`

### Requirement: Account area and sign-out

The shell SHALL show the signed-in user's name, role, and initials avatar, and SHALL provide a sign-out action that ends the session and returns to `/login`. When the menu is collapsed, only the avatar and a sign-out icon SHALL be shown.

#### Scenario: Sign out from any screen

- **WHEN** a signed-in user chooses Sign out from any shell screen
- **THEN** the session ends and the user is taken to `/login`
