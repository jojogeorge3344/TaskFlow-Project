# Admin Navigation Grouping

## Problem statement recap
The user wants grouped parent modules across all three roles, a linked Board screen with filters,
a new In-QA task status, and fixes for two sidebar UX issues:
- **Settings** parent → **Reminder Settings**, **Application Settings** (Admin)
- **Communication Management** parent → **Mail**, **Notifications** (Admin, Manager, Employee)
- **TaskFlow Management** parent → **Employee Management**, **Department Management** (Admin)
- **TaskFlow Monitoring** parent → **Task Management**, **Employee Workload**, **Board** (Admin, Manager; Employee gets **Task Management** parent → **My Tasks**, **Board**)
- Board view becomes a separate linked screen under the monitoring group with user-wise, status-wise and deadline from/to filters
- A new **In-QA** task status in the workflow
- Parent module labels must be fully readable (e.g. "Management" wraps below "Communication")
- Parent modules must always be collapsible (currently sometimes cannot be minimized)

## Goal
Reorganize the sidebar navigation for Administrator, Manager and Employee into parent modules for
better UX, surface the Board screen from the sidebar with filters, introduce the In-QA task status
across backend and frontend, and fix the parent-label wrapping and collapse defects - while
preserving all existing routes, guards, and behavior.

## Current state
- `nav-items.ts`: Admin nav grouped (Settings, Communication Management, TaskFlow Management) but
  Task Management / Employee Workload still top-level and Board not linked anywhere; Manager and
  Employee navs are flat.
- `KanbanComponent` exists at `/board` (all authenticated roles) but has no sidebar entry and no filters.
- `TaskStatus` enum: ToDo, InProgress, Blocked, Completed, Cancelled; fixed transition table in
  `TaskStatusTransitions`; kanban columns ToDo / In Progress / Blocked / Completed.
- Shell bug: a parent containing the active route cannot be collapsed (force-open logic).
- Shell bug: long parent labels such as "Communication Management" are truncated.

## Proposed change
1. **Sidebar grouping** - TaskFlow Monitoring parent (Task Management, Employee Workload, Board) for
   Admin+Manager; Task Management parent (My Tasks, Board) for Employee; Communication Management
   for all three roles; Settings parent for Admin (Reminder + Application) and Manager (Reminder).
2. **Board screen** - `/board` stays a separate screen, linked from all three sidebars, with
   client-side filters: Assignee (Admin/Manager only), Status (all statuses incl. In-QA), Deadline
   from/to, plus Clear filters and a filtered-empty state.
3. **In-QA status** - appended to `TaskStatus`; transitions InProgress ⇄ In-QA → Completed
   (plus In-QA → Blocked/Cancelled); kanban column and status pickers include it.
4. **UX fixes** - parent labels wrap to two lines when expanded; clicking a parent always collapses
   it; navigation to a child expands its parent once and the user's later collapse is respected.

## Impact
- Frontend: `nav-items.ts`, `shell.component.*`, `icon` usage (kanban icon exists), kanban component
  (filters), task models/status maps, task-details allowed-next, forms/status filters.
- Backend: `TaskStatus` enum (appended value), `TaskStatusTransitions`, transition tests, any
  exhaustive status switches (stage calculator, reports, workload, dashboard) must treat In-QA as an
  active status. No schema migration expected (enum stored as int, new value appended).
- Routes and role guards unchanged.
