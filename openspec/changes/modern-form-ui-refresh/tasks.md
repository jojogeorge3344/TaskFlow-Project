# Tasks

## 1. Design tokens and form primitives

- [x] 1.1 Add new tokens (field fill, hover border, soft focus ring, card radius, notice colours) to `:root` in `frontend/src/styles.css`, replace the hard-coded label colour with a token, and verify existing screens still build with `ng build`
- [x] 1.2 Restyle `.tf-field`, `.tf-input`, `.tf-select` (custom arrow, `appearance: none`), `.tf-textarea`, `.tf-input-group` and its suffix, `.tf-btn` and `.tf-check`, adding an icon-in-input variant and a visible invalid state with message; verify visually on the employee list filters, profile and a modal form
- [x] 1.3 Add `tf-card`, `tf-card__head`, `tf-notice` (warning, info, danger), `tf-option` pill picker, `tf-avatar`, `tf-pill`, `tf-meter` and `tf-alloc-bar` classes; verify by rendering each in the employee drawer in task 3
- [x] 1.4 Restyle `shared/position-combobox` and `shared/employee-picker` to use the shared tokens; verify their existing specs pass and the dropdowns match the new fields
- [x] 1.5 Run the frontend unit tests and confirm no spec fails from class or markup changes (`ng test`)

## 2. Drawer and modal shells

- [x] 2.1 Update `shared/ui/drawer.component.ts` template and `.tf-drawer` CSS: eyebrow plus title header, wider desktop width, scrolling body, sticky footer; verify its spec still passes for open, close, Escape, backdrop click and focus trap
- [x] 2.2 Apply the same header, body and sticky footer to `.tf-modal` and `confirm-dialog.component.ts`; verify the confirm dialog spec passes and the dialog renders correctly at 400px width
- [x] 2.3 Add a responsive rule so two-column `tf-form-grid` rows stack below 560px; verify at phone width with no horizontal scroll

## 3. Employee form panel

- [x] 3.1 Add the identity header (initials avatar, name, email, role and status pills, employee code) to `employee-form-panel.component.html` with a neutral add-mode state; verify with a spec for edit and add mode
- [x] 3.2 Regroup fields into four `tf-card` sections (personal details, role and team, project allocation, sign-in and security), keeping every control id, `formControlName` and `data-*` attribute; verify the existing `employee-form-panel.component.spec.ts` passes unchanged
- [x] 3.3 Convert department and manager-overseen-project checkboxes to `tf-option` pills with the checkbox kept in the DOM and a visible focus ring; verify toggle by click and keyboard in a spec
- [x] 3.4 Render derived managers as avatar chips with the existing empty-state hint; verify the `data-list="managers"` spec still passes
- [x] 3.5 Render the allocation capacity bar, remaining text and over-capacity message from the existing totals, restyle project rows with a colour dot, and keep Save disabled when over 100%; verify with specs for within capacity, over capacity and row removal
- [x] 3.6 Add the password show/hide toggle and the strength meter as local UI state, and replace the helper text with the sign-out warning callout in edit mode; verify validation errors are unchanged and the callout shows only when editing
- [x] 3.7 Restyle `employee-details-panel.component.ts` to match the new header and cards; verify it renders and its spec passes
- [x] 3.8 Resolve the "Last updated" open question: show the line only if the employee DTO already carries an updated timestamp, otherwise omit it; verify by reading the DTO and the rendered footer

## 4. Modal forms

- [x] 4.1 Apply cards, pills and notices to the department form dialog and the department members dialog; verify their specs pass and the dialogs match the employee drawer styling
- [x] 4.2 Apply the same to the project form dialog and project team dialog, replacing its local styles with shared classes where they duplicate them; verify specs pass
- [x] 4.3 Apply the same to the change-password form, including the show/hide and strength aids; verify the existing validation specs pass

## 5. Page forms

- [x] 5.1 Restyle the profile page into cards; verify the profile specs pass and the page matches the drawer styling
- [x] 5.2 Restyle application settings, AI provider settings, reminder settings and permissions management into cards with notices; verify each screen's specs pass
- [x] 5.3 Restyle task create, task details, reassign and discussion forms; verify the task specs pass
- [x] 5.4 Restyle mail compose and the recipient picker; verify the mail specs pass
- [x] 5.5 Restyle the filter bars (task list, reports, workload, dashboard filter bar) to use the new field styles without changing layout or behaviour; verify their specs pass

## 6. Integration checks

- [ ] 6.1 Walk every touched screen at desktop and 400px widths, checking focus rings, invalid states, sticky footers and the absence of horizontal scroll; record any screen that deviates and fix it
- [x] 6.2 Run the full frontend test suite and a production build, and confirm no backend files changed (`git status` shows only frontend and OpenSpec files)
- [x] 6.3 Update the assistant knowledge note `backend/src/TaskFlow.Infrastructure/Assistant/Knowledge/employee-management.md` only if it describes the old form layout; verify by reading it against the new screen

## 7. Edit panel is view-only for assignments

- [x] 7.1 Show departments and project allocations read-only (with notes pointing to Department and Project management) when editing, keep the editors only when adding, and verify with specs that edit mode has no department checkboxes, add-project control or rows
- [x] 7.2 Remove the sign-in and security section and password field when editing, keep a sign-out notice for username and role changes, and verify edit mode has no `#emp-password` and sends `password: null`
- [x] 7.3 Send loaded departments and allocations unchanged on save, skip allocation validation when editing, and verify with specs including a person whose recorded projects exceed 100%
- [x] 7.4 Update the form specs and run the full suite and `ng build`; verified: 905 tests pass

## 8. Department member picker

- [x] 8.1 Replace the "Manage members" text link on each department card with a members icon button (tooltip and aria-label kept), and verify the department list spec checks the icon button
- [x] 8.2 Rebuild the members dialog as one list of managers and employees with search by name or username, four sorts, multi-select, a selected count, and select-all-shown and clear actions; verify with dialog specs for search, sort, hidden selections and bulk select
- [x] 8.3 Save managers and employees separately by role while keeping inactive current members; verify the replace-members payload in specs and a passing `ng build`

## Workflow follow-up

- Archive the change after review and manual UI sign-off.
