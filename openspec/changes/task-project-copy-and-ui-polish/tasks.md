# Tasks

## 1. Backend: project and reminder fields on task DTOs

- [x] 1.1 Add `ProjectId` and `ProjectName` to `TaskListItemDto` and `TaskDetailsDto` and fill them in the `TaskService` mappers with a left join to projects. Verify: a service test returns the project name for a task with a project and null for a legacy task, and the list query still runs as one query.
- [x] 1.2 Add `FirstReminderDays`, `SecondReminderDays` and `OverdueReminderFrequency` to `TaskDetailsDto` (needed to pre-fill a copy; the current DTO does not expose them). Verify: a service test shows the values stored at creation come back from `GET /tasks/{id}`.
- [x] 1.3 Mirror the new fields in `task.model.ts` (`TaskListItem`, `TaskDetails`). Verify: `ng build` compiles with no type errors.

## 2. Return-URL helper and Back button

- [x] 2.1 Add a shared helper that validates a `returnUrl` (single leading `/`, no `//`, no scheme) and returns a path plus a label ("Back to project", "Back to tasks", "Back to board"). Verify: unit tests cover valid paths, `//evil.com`, `http://x`, `javascript:`, empty and null.
- [x] 2.2 Add `returnUrl` to task links in the Task list, Project detail and Kanban, and preserve it on the discussion, activity, reassign and post-save navigations. Verify: component specs assert the link query params and that sub-screen links keep the parameter.
- [x] 2.3 Replace `backToBoard()` in `task-details` with the helper-driven Back, defaulting to `/tasks`, and use its label in the template. Verify: specs for each entry point, direct open and an external `returnUrl`; manually open a task from a project, click Back, and land on that project.

## 3. Project on the Task list and task screen

- [x] 3.1 Add a Project column to the Task list table and cards, showing "—" when null. Verify: task-list spec renders both cases.
- [x] 3.2 Add the Project filter (load via `ProjectService.getAll()`, hide on failure), wire `projectId` into the request, reset to page 1 and include it in `hasFilters` and reset. Verify: specs assert the request, the page reset, and the reset button.
- [x] 3.3 Show the project name read-only in the task screen header and in the edit form, and confirm the update request does not send a project. Verify: task-details spec asserts the text and the request body.

## 4. Copy task

- [x] 4.1 Add a Copy action to Task list rows and the task screen, shown only with the create-task permission, linking to `/tasks/new?copyFrom=<id>`. Verify: specs for visible and hidden states and the link target.
- [x] 4.2 Pre-fill `task-create` from `copyFrom` (title, description, project, assignee when still eligible, priority, hours, recipients, reminders), keep new-task date defaults, let `?projectId=` take precedence, and show a message if the source cannot be loaded. Verify: specs for full prefill, ineligible assignee, 404 source, and that no create request is sent until submit.

## 5. Task edit screen redesign

- [x] 5.1 Rebuild the edit form markup in `task-details` as sectioned `tf-card` blocks (header, Details, Schedule) with a sticky action bar and a responsive grid, keeping `editForm`, validators, control ids and `data-*` attributes. Verify: existing task-details specs pass unchanged.
- [ ] 5.2 Add responsive and error-placement checks. Verify: a layout spec asserts one-column stacking at the small breakpoint and the deadline error next to its field, and a manual pass at 360px and 1440px shows no horizontal scroll.

## 6. Team dialog alignment

- [ ] 6.1 Drive the heading row and data rows from one shared column template, switch headings to non-wrapping sentence case, size the percent and manager columns to their headings, and centre the manager checkbox. Verify: a dialog spec asserts both rows use the same template and a manual check shows headings over their inputs at 46rem.
- [ ] 6.2 Add the narrow-width layout where rows stack and each field has a visible label. Verify: manual check at 400px shows labelled fields and no horizontal scroll; existing project-team-dialog specs pass.

## 7. Profile page layout

- [ ] 7.1 Rework the profile grid so sections fill the width in two or three balanced columns, add compact empty states for sparse sections, and collapse to one column at phone width. Verify: the profile layout spec asserts the column counts at the breakpoints and existing profile specs pass.
- [ ] 7.2 Manually review the profile page at 360px, 768px, 1440px and 1920px for empty regions and horizontal scroll. Verify: no large empty blocks and no horizontal scroll at any width.

## 8. Final verification

- [x] 8.1 Run the full frontend and backend test suites and fix regressions in the specs of the two overlapping open changes. Verify: both suites pass.
- [ ] 8.2 Walk the six original requests end to end in the running app: project filter and project name, copy, edit-screen design, team dialog headings, Back from a project's task, and the profile page. Verify: each behaves as the specs describe.
