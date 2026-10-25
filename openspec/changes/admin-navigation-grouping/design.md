# Design - Admin Navigation Grouping

## Context
Group sidebar navigation items under parent modules for all three roles, link the existing Board
screen from the sidebar with filters, add an In-QA task status, and fix two shell defects.
Implementation lives in `nav-items.ts`, `shell.component.*`, `KanbanComponent`, the task models, and
backend domain rules under `TaskFlow.Domain` / `TaskFlow.Infrastructure`.

## Goals
Role-appropriate grouped sidebars, a filterable Board screen, an In-QA workflow status, and a shell
that wraps long parent labels and always collapses - with no route or guard changes.

## Approach

### 1. NavItem hierarchy (done)
`NavItem` has an optional `children?: NavItem[]`; the shell renders entries with children as
expandable groups (`<button class="nav__parent">` + `.nav__children`).

### 2. Sidebar structures (revised)

Administrator: Dashboard, **TaskFlow Monitoring** (Task Management `/tasks`, Employee Workload
`/workload`, Board `/board`, icon `kanban`), **TaskFlow Management** (Employee Management,
Department Management), **Communication Management** (Mail w/ badge, Notifications), Reports,
**Settings** (Reminder Settings, Application Settings), My Profile.

Manager: Dashboard, **TaskFlow Monitoring** (Task Management, Employee Workload, Board),
**Communication Management** (Mail w/ badge, Notifications), Reports, **Settings** (Reminder
Settings), My Profile.

Employee: Dashboard, **Task Management** (My Tasks `/tasks`, Board `/board`),
**Communication Management** (Mail w/ badge, Notifications), My Profile.

### 3. Board screen with filters
`/board` route and guard stay as-is (all authenticated roles). `KanbanComponent` gains a filter bar,
all applied client-side over the already-loaded board list (the endpoint returns every task the
caller may access, so no backend change is needed):
- **Assignee** dropdown - populated from the existing users service; shown for Administrator and
  Manager only (an Employee's board only contains their own tasks).
- **Status** dropdown - all statuses including In-QA; empty means all.
- **Deadline from / to** date inputs - inclusive range on task deadline.
- Clear filters link; a dedicated empty state when filters match nothing.
Filter changes re-group the column list; drag-and-drop keeps working on the filtered view
(backend rejects disallowed transitions and the card rolls back as today).

### 4. In-QA task status
- Append `InQA` **after** `Cancelled` in the `TaskStatus` enum so existing stored integer values
  keep their meaning (no migration).
- Transitions (`TaskStatusTransitions`): `InProgress -> InQA` added; `InQA -> InProgress`
  (send back on failed QA), `InQA -> Completed`, `InQA -> Blocked`, `InQA -> Cancelled`; To Do
  cannot go directly to In-QA; Completed/Cancelled remain terminal.
- Status must count as **active/open** everywhere the code currently checks
  `Status != Completed && Status != Cancelled` (workload, reports, dashboard, stage calculator) -
  those exclusion-based checks pick In-QA up automatically; any exhaustive `switch` on status must
  be extended and covered by tests.
- Frontend: add `'InQA'` to `TASK_STATUSES` and to `BOARD_STATUSES` after `InProgress`; label
  `"In QA"` in the kanban/task-list label maps; kanban column order becomes ToDo, In Progress,
  In QA, Blocked, Completed; `task-details` allowed-next gains In-QA (from In Progress) and an
  In-QA case returning `[InProgress, Blocked, Completed, Cancelled]`; status filters and pickers
  driven by `TASK_STATUSES` pick it up automatically.

### 5. Parent label wrapping
Expanded-sidebar parent labels must wrap onto multiple lines (e.g. "Communication" / "Management")
instead of truncating; the collapsed-rail tooltip keeps the full single-line label. CSS only
(`white-space: normal` on `.nav__parent-label`, sensible max width); no template change needed.

### 6. Collapse behavior fix
Replace the force-open logic in `isGroupOpen` (which kept a group open whenever one of its children
was the active route, making it impossible to minimize). New model:
- `groupState` records the user's explicit toggles; the default is minimized, so on login every
  parent module renders collapsed (only the group containing the active route may start open).
- On each `NavigationEnd`, the group containing the new active route is expanded once (so
  navigation into a child is always visible), but a later manual toggle of that group is
  respected until the next navigation.
- Clicking a parent always toggles it, including while one of its children is active.

### 7. Preserve routes and guards
No route changes. `/board` keeps its "any authenticated role" registration; all child screens keep
their existing guards.
