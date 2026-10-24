# Proposal

## Why

Comments posted from a task's Discussion screen are not shown back in that screen's list, so users cannot see the conversation they just contributed to. Participants also get no signal when a discussion changes, and the discussion is currently add-only with no way to correct or remove a comment.

## What Changes

- Fix the Discussion screen (`/tasks/:id/discussion`) so every comment saved for a task is listed for that task, oldest first, including after a reload and after posting. The task's Edit/Details screen keeps listing the same comments.
- Allow a comment's author to **edit** and **delete** their own comment from the Discussion screen (**BREAKING**: comments were previously immutable, per `task-comments`).
- When a comment is added, edited or deleted, send an in-app notification and an Inbox mail to the task's assigned members (the task's recipients, which always include the assignee), and to the user who made the change.
- Record comment edits and deletions in the task's Activity History alongside the existing "comment added" event.

## Capabilities

### New Capabilities
- `task-discussion`: Listing a task's discussion comments per task, and authors editing/deleting their own comments.
- `task-discussion-notifications`: Notifying a task's assigned members by in-app notification and Inbox mail when a discussion comment is added, edited or deleted.

### Modified Capabilities
<!-- None: the earlier task-tracking-impl `task-comments` delta is an unarchived change, not a main spec; this change's requirements supersede its immutability rule. -->

## Impact

- Backend: `TaskCommentService`/`ITaskCommentService` (update, delete, notify), `TasksController` (new `PUT`/`DELETE /api/tasks/{id}/comments/{commentId}`), `NotificationType` (new comment type), `TaskActivityEventType` (edit/delete events), `TaskComment` entity (edit timestamp, soft delete) with an EF migration, permission catalog (comment edit/delete).
- Frontend: `task-discussion.component.*`, `task.service.ts`, `task-details.component.*`, models.
- Tests: backend `TaskCommentActivityNotificationTests`, frontend `task-discussion.component.spec.ts`.
- Assumptions: "email" means TaskFlow's internal Inbox mail (the project has no SMTP integration); "assigned members" means the task's recipients; only a comment's author may edit/delete it; deletions are soft so Activity History stays intact.
