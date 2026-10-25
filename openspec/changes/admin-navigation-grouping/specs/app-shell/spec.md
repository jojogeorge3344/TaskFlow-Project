# App Shell Delta Spec

## ADDED Requirements

### Requirement: Parent modules for Administrator sidebar
The Administrator sidebar SHALL group screens under parent modules.

#### Scenario: Administrator grouped navigation
- **WHEN** a user with the Administrator role opens the application shell
- **THEN** the sidebar shows, in order: Dashboard, a **TaskFlow Monitoring** parent containing Task Management (`/tasks`), Employee Workload (`/workload`) and Board (`/board`), a **TaskFlow Management** parent containing Employee Management (`/employees`) and Department Management (`/departments`), a **Communication Management** parent containing Mail (`/mail`, keeping the unread badge) and Notifications (`/notifications`), Reports, a **Settings** parent containing Reminder Settings (`/settings/reminders`) and Application Settings (`/settings/application`), and My Profile.

### Requirement: Parent modules for Manager sidebar
The Manager sidebar SHALL group screens under parent modules.

#### Scenario: Manager grouped navigation
- **WHEN** a user with the Manager role opens the application shell
- **THEN** the sidebar shows: Dashboard, **TaskFlow Monitoring** (Task Management, Employee Workload, Board), **Communication Management** (Mail with badge, Notifications), Reports, **Settings** (Reminder Settings), and My Profile.

### Requirement: Parent modules for Employee sidebar
The Employee sidebar SHALL group screens under parent modules.

#### Scenario: Employee grouped navigation
- **WHEN** a user with the Employee role opens the application shell
- **THEN** the sidebar shows: Dashboard, a **Task Management** parent containing My Tasks (`/tasks`) and Board (`/board`), **Communication Management** (Mail with badge, Notifications), and My Profile.

### Requirement: Parent labels wrap instead of truncating
A parent module's label SHALL wrap onto multiple lines when fully visible rather than being truncated.

#### Scenario: Long parent label is fully readable
- **WHEN** the sidebar is expanded and a parent label such as "Communication Management" does not fit on one line
- **THEN** the label wraps (e.g. "Management" on a second line) and the full wording remains visible, while the collapsed rail tooltip still shows the single-line label.
### Requirement: Parent modules start minimized

All grouped parent modules SHALL start collapsed when the shell loads, so a freshly logged-in user sees only the top-level entries until they expand a group themselves.

#### Scenario: Login shows every group minimized

- **WHEN** a user logs in and the application shell renders
- **THEN** every parent module renders collapsed (`aria-expanded="false"`) with no child links in the DOM, except the group containing the active route if it is a child route

#### Scenario: Clicking a minimized parent expands it

- **WHEN** the user clicks a collapsed parent module
- **THEN** the group expands and its child links become visible

### Requirement: Parent modules are always collapsible

Clicking a parent module SHALL toggle its expanded/collapsed state, including when one of its children is the active route.

#### Scenario: Collapse an active group
- **WHEN** the user is on a child route (e.g. `/employees`) so its parent is expanded, and clicks the parent
- **THEN** the group collapses and stays collapsed until the user expands it again.

#### Scenario: Navigation expands the destination group once
- **WHEN** the user navigates to a child route (by any means, including from another screen) while its parent group is collapsed
- **THEN** the group containing the active route is expanded, and a subsequent manual collapse of that group is respected until the next navigation.

### Requirement: Preserve existing routes and guards
All existing routes SHALL continue to work and all existing role guards and permissions SHALL remain unchanged; sidebar grouping only changes presentation.

#### Scenario: Unchanged access control
- **WHEN** any previously reachable screen is opened by its existing URL or sidebar entry
- **THEN** the same role guard decisions apply as before the grouping change, and the Mail badge continues to show the unread count.
