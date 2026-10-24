# Tasks

## 1. Board project filter

- [x] 1.1 Add `ProjectId` and `ProjectName` to `TaskCardDto`, include `Project` in `GetBoardAsync` and map both in `ToCard`; verify `dotnet build` passes and a TaskService test asserts board cards carry the project id and name
- [x] 1.2 Add `projectId` and `projectName` to the frontend `TaskCard` model and fix any test fixtures that build cards; verify `ng build` type-checks and existing kanban specs compile
- [x] 1.3 In `kanban.component`, load projects with `ProjectService.getAll()`, add a `projectId` signal, a Project select with "All projects", wire it into `matches()`, `hasFilters` and `clearFilters()`, and show the project name on each card; verify with kanban specs for choosing a project, combining it with status, Clear resetting it, and the no-match message

## 2. Team dialog manager label

- [x] 2.1 Remove the visible `<span>Manager</span>` from the team row in `project-team-dialog.component.ts`, keeping the checkbox, `aria-label`, `title` and disabled state, and tidy any CSS that sized the label; verify a dialog spec asserts the row has the checkbox and no "Manager" text, and existing project-team-dialog specs still pass

## 3. Profile lists

- [x] 3.1 Change `readonlyFields()` in `profile.component.ts` to return a list of values per field, with one item per allocation for Projects (name and percent) and one per name for Departments, Managers, Managed projects and Team; verify unit specs for several values, one value and no value ("—")
- [x] 3.2 Render multi-value fields as a bulleted `<ul>` in `profile.component.html`, keep the total allocated percentage visible, and style the list in `profile.component.css`; verify a spec asserts one bullet per value, plain text for a single value, and no comma-joined text

## 4. Profile column layout

- [x] 4.1 Reorder the profile template so Account details is the left column and Personal information then Password are the right column, remove the mobile Profile/Security tab switcher so sections stack in one column on small screens, and remove styling that leaves empty space under Personal information; verify `profile.component.layout.spec.ts` asserts the column placement, the stacking order and that the tab switcher is gone
- [x] 4.2 Reconcile with `task-project-copy-and-ui-polish` task 7.1 so only one profile grid definition remains; verify existing profile specs pass and the unfinished 7.1 wording no longer conflicts

## 5. Integration check

- [ ] 5.1 Run `dotnet test` and the frontend test suite; verify both pass
- [ ] 5.2 Walk the four changes in the running app at 360px and 1440px: board project filter, team dialog, profile lists, profile columns; verify each behaves as the specs describe and the page has no horizontal scroll
