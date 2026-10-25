# Proposal

## Why

Four small gaps in the UI make everyday screens harder to use: the board cannot be narrowed to one project, the project team dialog repeats the word "Manager" next to a column already headed "Project manager", the profile joins several projects, teams, departments and managers into one comma-separated line, and the profile page splits its information awkwardly. They are independent of the larger architecture refactor and can ship now.

## What Changes

- Board: add a **Project** filter next to Assignee, Status and Deadline. Board cards need the project, so the board payload gains the project id and name.
- Project team dialog: remove the visible "Manager" text beside each row's checkbox. The checkbox, its rules (Manager role only, at least one manager per non-empty team) and its accessible label stay.
- Profile: show Projects, Departments, Managers, Managed projects and Team as bulleted lists, one item per line, instead of comma-joined text. A single value or no value still reads cleanly.
- Profile: put **Account details** in the left column and **Personal information** plus **Password** in the right column, with no extra empty space under Personal information.

## Capabilities

### New Capabilities
- `board-project-filter`: filtering the Kanban board by project, and showing each card's project.
- `project-team-dialog-manager-control`: how the per-row manager control is presented in the project team dialog.
- `profile-multi-value-display`: how a profile shows fields that hold several values.
- `profile-account-layout`: which profile sections sit in the left and right columns.

### Modified Capabilities
<!-- None: the main specs folder is empty; related requirements live in other in-flight changes and are not edited here. -->

## Impact

- Backend: `TaskCardDto`, `TaskService.GetBoardAsync`/`ToCard` (add project id and name); no database change, no new endpoint.
- Frontend: `kanban.component.*`, `core/models/task.model.ts` (`TaskCard`), `project-team-dialog.component.ts`, `profile.component.*`.
- Overlap: the in-flight `task-project-copy-and-ui-polish` change has an unfinished profile grid task (7.1) and team-dialog alignment tasks. This change overrides the profile column placement from 7.1; apply the two together or finish 7.1 first.
- Out of scope: activity/discussion/reassign problems and the repository-pattern refactor, which are separate changes.
