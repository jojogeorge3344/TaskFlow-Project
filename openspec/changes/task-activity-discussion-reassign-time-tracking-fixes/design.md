# Design

## Context

See proposal.md for motivation. From exploration:
- Comments live on separate route `tasks/:id/discussion` (task-discussion.component.*); task-details doesn't show them inline - need to surface them on Edit Task screen.
- ActivityComponent doesn't initialize from URL query params; filters exist in UI but aren't applied from navigation (e.g., "Time tracking" link prefilter).
- ReassignAsync already logs `TaskActivityEventType.Reassigned` with old/new assignee; likely visibility issue is filtering/permissions or not displayed properly.
- Time tracking: `HoursLogged` exists; current "Time tracking" link points to `eventType=Administrative` with `search=hours` (wrong). Need proper time tracking UI in Links tab, log hours (e.g., `HoursLogged`/`TimeLogged`), compute remaining = estimated - actual (handle nulls), ensure events appear in Activity History.

## Goals / Non-Goals

**Goals:**
- Show discussion comments on Edit Task screen with "No comments yet" state.
- Make Activity History filters work including URL query params.
- Ensure reassign events appear in Activity History.
- Add proper time tracking in Links tab, show remaining/actual hours, log to activity and display in Activity History.

**Non-Goals:**
- Redesigning discussion UI beyond visibility.
- Changing activity event schema broadly.

## Decisions

- **Comments placement**: Surface comments on task-details (inline/list) near tabs/context; fetch via existing discussion service/API or reuse task discussion data. Keep discussion route intact.
- **Activity filters**: In ActivityComponent, read `ActivatedRoute.queryParamMap` and initialize filter model; ensure service passes filters to backend; verify backend filtering supports eventType/date/user/search.
- **Reassign visibility**: Confirm Reassigned events are queryable and displayed (no schema change needed if already logged). Fix any filter/permission issues.
- **Time tracking**: Add time tracking controls in Links tab (input hours, date/notes), call API to log hours (use existing hours logging path), compute remaining hours in task-details from estimated/actual, ensure logged events use correct event type and appear in activity.

## Risks / Trade-offs

- [Risk] Backend filtering might not support all params → Mitigation: align frontend params with backend API.
- [Risk] Hours logging endpoint/type mismatch → Mitigation: use existing `HoursLogged`/`UpdateAsync` paths and verify event type.
- [Trade-off] Inline comments vs separate route: show summary/list on details for visibility while keeping full discussion page.
