# Proposal

## Why

The Edit employee drawer is a single stack of plain inputs, plain checkboxes and loose project rows, and the same flat look repeats across the other forms and dialogs. Almost every form screen already builds on the shared `tf-*` classes in `frontend/src/styles.css` and on the shared `tf-drawer` and `tf-modal` shells, so one restyle there can give every screen a consistent, modern look instead of redesigning screens one by one.

## What Changes

- Restyle the shared form primitives in `styles.css`: `tf-field`, `tf-input`, `tf-select`, `tf-textarea`, `tf-input-group`, `tf-check`, `tf-chip`, `tf-btn`. This covers filled fields, a clearer focus ring, a custom select arrow, an attached suffix, and a distinct invalid state.
- Add reusable shell parts: a header with eyebrow, title and close button; a sticky footer; a `tf-card` form section with a title and a hint; a `tf-notice` callout (warning, info, danger); `tf-option` pill pickers for multi-select; an identity header with avatar and pills.
- Rebuild the Edit/Add employee drawer from the approved mockup: identity header, four cards (personal details, role and team, project allocation, sign-in and security), selectable department pills, avatar chips for derived managers, a segmented allocation bar with a live over-allocation state, a password show/hide button with a strength meter, and an amber sign-out callout.
- Apply the same primitives and card grouping to the other form screens:
  - Modal forms: department, project, department members, project team, change password.
  - Page forms: profile, application settings, AI provider, reminders, permissions, task create and details, mail compose.
- Keep every existing CSS class, `data-*` attribute, form control id and behaviour. New classes are additive, so existing specs keep passing.
- Out of scope: tables, list pages, dashboard cards and charts, a dark theme, avatar photo upload, and any backend change.

## Capabilities

### New Capabilities
- `form-ui-system`: the shared visual and interaction rules for forms, drawers and dialogs (field styling, focus and invalid states, sectioned cards, pill pickers, callouts, sticky footers, responsive stacking, accessibility), plus the employee form layout that follows them.

### Modified Capabilities
<!-- None. The project has no existing specs under openspec/specs/, and no business behaviour changes. -->

## Impact

- `TaskFlow Proj/frontend/src/styles.css`: restyled primitives and new shared classes.
- `TaskFlow Proj/frontend/src/app/shared/ui/drawer.component.ts` and `confirm-dialog.component.ts`: shell header and footer markup.
- `TaskFlow Proj/frontend/src/app/features/employees/`: `employee-form-panel.component.html`, `employee-details-panel.component.ts` and its styles.
- Markup touch-ups in the department, project, profile, settings, tasks and mail form templates listed above.
- `shared/position-combobox` and `shared/employee-picker`: restyle to match the new fields.
- No API, database or dependency changes. Existing component specs that query classes or `data-*` attributes must keep passing.

## Assumptions

- All form screens ship in one change, because most of the work is shared CSS.
- Only forms and dialogs are refreshed. Lists and dashboards are untouched.
- The refreshed look is light only. Tokens may be dark-ready, but no theme toggle ships.
- Avatars show initials only.
- The "Last updated" line in the drawer footer is shown only if the API already returns that value.
