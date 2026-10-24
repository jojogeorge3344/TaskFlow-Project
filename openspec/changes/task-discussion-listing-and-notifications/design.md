# Design

## Context

See proposal.md. Observed: `GET /api/tasks/{id}/comments` returns comments via `TaskCommentService.GetForTaskAsync`, and the Details screen lists them, but the Discussion screen (`task-discussion.component.ts`) reportedly does not. The read path looks correct in code, so the root cause is not yet confirmed (candidates: route `id` handling and reload in `ngOnInit`, response shape vs the `TaskComment` model, query filtering, or the `CanAccessTaskAsync` check). Comments are currently immutable; `TaskCommentService.AddAsync` only logs a `CommentAdded` activity. Notifications go through `INotificationPublisher` (skips when the in-app setting is off). Internal mail is built from `MailMessage`/`MailRecipient`/`MailboxItem` rows (see `ReminderService.TryDeliverAsync`, system sender, `IsSystem = true`); there is no SMTP. Task recipients live in `TaskRecipients` (the assignee is always included).

## Goals / Non-Goals

**Goals:**
- Reproduce and fix the listing bug at its root cause, with a regression test.
- Add edit/delete of own comments and notify recipients (notification + Inbox mail) on add/edit/delete.

**Non-Goals:**
- External email/SMTP, admin moderation of others' comments, comment threading, mentions.

## Decisions

- **Root cause first**: reproduce against the running app/API before coding and fix what is actually wrong. Alternative (reload the list after every post) would mask the defect.
- **Soft delete + EditedAt**: add `EditedAt` and `IsDeleted` to `TaskComment` (EF migration); `GetForTaskAsync` filters deleted rows. Preserves Activity History references; hard delete rejected.
- **Endpoints**: `PUT` and `DELETE /api/tasks/{id}/comments/{commentId}`, author-only check in the service, guarded by new permissions (comment edit/delete) added to the permission catalog and granted to roles that hold `TasksCommentCreate`. Reusing the create permission was rejected to keep role configuration granular.
- **Notification fan-out in `TaskCommentService`**: a private helper loads `TaskRecipients` for the task (plus assignee), includes the actor and excludes soft-deleted users, publishes a new `NotificationType` for comments via `INotificationPublisher`, and writes one system Inbox mail per recipient using the same entity pattern as reminders. Subject names the task code/title and the action; body names the author and a trimmed comment excerpt.
- **Activity events**: add `CommentEdited` and `CommentDeleted` to `TaskActivityEventType`.
- **Frontend**: Discussion shows Edit/Delete only on the current user's comments, inline edit box, delete confirmation, and updates the signal list from the API result.

## Risks / Trade-offs

- Root cause may sit outside the files listed → reproduce first and widen scope only as evidence requires.
- Enum additions are persisted → verify the EF mapping and migration before relying on them.
- Fan-out happens after the comment save; a notification failure must not roll back the comment → catch and log delivery errors, covered by a test.
- Notification volume on busy tasks (one row per recipient per change) → acceptable at this scale.
