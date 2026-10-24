## Context

The task screen has a sidebar form for logging hours and an edit form with a Worked hours field. Both save through `PUT /tasks/{id}`, which sets `Task.ActualHours`, appends a `HoursLogged` activity entry and sends no notification. Internal mail (`IMailService.SendAsync`) addresses recipients by email and sends as a user.

## Goals / Non-Goals

**Goals**
- Log hours reliably, without a reload.
- Keep every on-screen worked value in sync.
- Notify assigned members on every add, update or clear.

**Non-Goals**
- A per-entry time-log table.
- A new notification type or a database migration.

## Decisions

1. **Blank-screen cause and fix.** `<form (ngSubmit)>` only works with `FormsModule` or a reactive `[formGroup]`. The component imports only `ReactiveFormsModule`, so the native submit reloads the page. Replace it with `<form (submit)="$event.preventDefault(); logHours()">`. That avoids pulling in `FormsModule`.
2. **Stale field.** After `logHours` succeeds, `task.set(updated)` and, if `editing()`, `editForm.patchValue({ actualHours: updated.actualHours })`. This matches the existing pattern in `startEdit`.
3. **Clear semantics.** `saveEdit` sends `actualHours ?? 0` so a cleared field means 0. The backend's `HasValue` guard stays, which protects other callers that omit the field. Alternative: a `ClearActualHours` flag. That adds API surface for no gain.
4. **Notification.** In `UpdateAsync`, after `SaveChangesAsync`, call a private `NotifyHoursChangedAsync(task, recipients, oldHours, newHours, actorId)`. Its targets are the assignee plus `recipients`, de-duplicated (the user who made the change is included). For each target it calls `INotificationPublisher.PublishAsync(..., NotificationType.System, msg)`. It sends one internal mail through `IMailService.SendAsync(actorId, ...)` addressed to the targets' emails, loaded from `Users` and skipping inactive or deleted users. Message text distinguishes added, updated and cleared.
5. **Failure isolation.** Notification and mail run in try/catch with logging. A failure never fails or rolls back the hours update.
6. **Change detection.** Notify only when the total actually changed, reusing the existing `task.ActualHours != request.ActualHours` check.

## Risks / Trade-offs

- Internal mail's `SendAsync` validates recipients and subject, so the mail is addressed by email. A user without an email is skipped.
- Mail is sent as the acting user, so it appears in their Sent folder. Acceptable, and it is how mail works today.
- `TaskService` gains an `IMailService` dependency. Check that the DI graph has no cycle (`MailService` does not use `TaskService`).
- A cleared total of 0 is indistinguishable from "never logged". Accepted.
