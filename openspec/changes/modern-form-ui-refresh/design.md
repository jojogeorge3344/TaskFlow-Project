# Design

## Context

The Angular frontend styles forms through global `tf-*` classes in `frontend/src/styles.css` (about 1300 lines, with `--tf-*` tokens). Around 25 templates use `tf-field`, `tf-input` or `tf-select`. Two shared shells exist: `tf-drawer` (`shared/ui/drawer.component.ts`, used by employees) and native-`<dialog>` `tf-modal` (department, project, change password, confirm). Only a few components carry local styles (charts, employee-picker, account-recovery, project-team, employee-details). The app is light-only. See proposal.md for motivation and scope.

The mockup (`employee-edit-mockup.html`, scratchpad) is the visual reference.

## Goals / Non-Goals

**Goals:**
- One look for all forms, achieved mainly by restyling shared primitives.
- Employee drawer rebuilt from the mockup with the same form logic.
- No change to existing class names, `data-*` attributes, ids or `aria-*` wiring.

**Non-Goals:**
- Dark theme, tables, list pages, dashboards and charts.
- Avatar upload, backend or API changes.
- Replacing the component structure or moving to a UI library.

## Decisions

1. **Restyle in place, add new classes.** Update the existing `.tf-field`, `.tf-input`, `.tf-select`, `.tf-textarea`, `.tf-input-group`, `.tf-btn`, `.tf-check` rules so every screen inherits the look; introduce `tf-card`, `tf-card__head`, `tf-notice`, `tf-option`, `tf-avatar`, `tf-pill`, `tf-meter`, `tf-alloc-bar` for new structures.
   *Alternative:* per-screen CSS or a new component library. Rejected: it drifts, costs more, and breaks the specs that query existing classes.

2. **Tokens first.** Extend the `--tf-*` set (field fill, hover border, soft focus ring, card radius, notice colours) in `:root`. Keep the names of current tokens. Token values are written so a dark set could be added later, but none ships.
   *Alternative:* hard-coded values per rule. Rejected: this is how the inconsistency arose (`#334155` is hard-coded in `.tf-field > label`).

3. **Select styling.** Use `appearance: none` with an inline SVG arrow background, so native keyboard and screen-reader behaviour is retained. Do not replace selects with a custom listbox.
   *Alternative:* a custom dropdown. Rejected: more code and accessibility risk, with no behaviour benefit.

4. **Pill pickers wrap real checkboxes.** `tf-option` is a label around a visually hidden `<input type="checkbox">` followed by a styled span. The existing `data-department` and `data-project` attributes stay on the input, so specs and keyboard behaviour are unchanged.

5. **Drawer and modal shells.** The header gets an eyebrow and the footer becomes sticky inside `tf-drawer` and `tf-modal`. `DrawerComponent` keeps its inputs/outputs, focus trap and Escape handling; only its template markup and CSS change. `tf-drawer` widens from 28rem to about 38rem on desktop and stays full width on phones.

6. **Identity header is presentational.** It reads the already-loaded employee (name, email, role name, active flag, code) inside the form panel. Initials are computed in the component. Add mode renders a neutral header. The "Last updated" line renders only if the existing employee DTO already carries a timestamp; no API change.

7. **Allocation bar derives from existing signals.** `rows()`, `totalPercent()`, `overAllocated()` and `remainingPercent()` already exist in the form panel. The bar and the over-capacity message render from them; segment colours cycle through a fixed palette by row index. Existing `data-allocation-summary` and `data-allocation-error` attributes stay.

8. **Password aids are local UI state.** A show/hide toggle flips the input `type`; the strength meter is a pure function of the typed value and never feeds validation. Existing validators and error texts are unchanged.

9. **The edit panel is view-only for departments and projects and has no password.** Assignments belong to Department management and Project management, so `employee-form-panel` renders departments and project allocations as read-only lists when editing and keeps the editors only when adding. The loaded departments and allocations are still sent unchanged on save, and allocation validation is skipped when editing so a person with legacy data can still be saved. The sign-in section and password field exist only when adding; editing keeps a sign-out notice. "Projects this manager oversees" was kept editable here at first; the `project-team-manager-flag` change makes it read-only because the project team dialog now sets it.

10. **Roll-out order:** primitives and shells, then the employee drawer, then modals (department, project, change password, team and members dialogs), then page forms (profile, settings, tasks, mail). Each step is shippable on its own.

## Risks / Trade-offs

- [Global restyle shifts layouts on screens not touched] → Check each listed screen visually after the primitives change; keep spacing tokens unchanged; fix in the same step.
- [Specs depend on class names or DOM order] → Only add classes and wrappers that keep existing selectors valid; run the frontend test suite after each step.
- [Local component styles override new primitives (employee-picker, position-combobox, project-team)] → Restyle those components to use the shared tokens in the same change.
- [Pill pickers hide the native checkbox] → Keep the input in the DOM, focusable, with a visible `:focus-visible` ring on the pill.
- [Wider drawer crowds small laptops] → Cap with `max-width: 100%` and stack the grid below 560px.
- [Colour-only states] → Pair error colour with message text and an icon or text label.

## Open Questions

- Whether the employee DTO exposes an updated-at value for the footer line (decided when the form panel is implemented; drop the line if absent).
