# Tasks

## 1. Fix Discussion listing

- [x] 1.1 Reproduce the missing list (post a comment, open `/tasks/:id/discussion`, inspect the `GET .../comments` response and component state) and note the root cause in the commit message; verify by observing the failure before the fix
- [x] 1.2 Fix the root cause in `task-discussion.component.ts`/`.html` and/or `TaskCommentService.GetForTaskAsync`, and add a component test that renders saved comments for the route's task and keeps a posted comment after reload; verify the spec passes with `ng test`
- [x] 1.3 Add a backend test that comments are returned only for their task, oldest first; verify `dotnet test --filter TaskCommentServiceTests` passes

## 2. Edit and delete comments (backend)

- [x] 2.1 Add `EditedAt`/`IsDeleted` to `TaskComment`, include edit state in `TaskCommentDto`, create the EF migration, and verify the migration applies and `GetForTaskAsync` excludes deleted comments
- [x] 2.2 Add `UpdateAsync`/`DeleteAsync` to `ITaskCommentService` (author-only, non-empty text, task must exist) with `CommentEdited`/`CommentDeleted` activity events; verify unit tests cover author success, non-author rejection and empty text
- [x] 2.3 Add `PUT`/`DELETE /api/tasks/{id}/comments/{commentId}` to `TasksController` with new permission keys in the catalog and role seeds; verify controller tests and the updated `PermissionCatalogTests` pass

## 3. Notify assigned members

- [x] 3.1 Add a comment `NotificationType` and a recipient fan-out helper (task recipients plus assignee, excluding the actor and deleted users) that publishes in-app notifications and system Inbox mail for add/edit/delete; verify with tests asserting rows for each recipient and none for the actor
- [x] 3.2 Ensure a delivery failure or a disabled in-app preference never discards the comment change; verify with tests for both cases

## 4. Discussion UI for edit/delete

- [x] 4.1 Add `updateComment`/`deleteComment` to `task.service.ts` and the models; verify the service spec asserts the URLs and verbs
- [x] 4.2 Add inline edit, delete confirmation and an "edited" marker to the Discussion screen, shown only on the current user's comments and gated by the new permissions; verify component tests for edit, delete and hidden controls on others' comments
- [x] 4.3 Keep the task Details comments list consistent with edits and deletes; verify its spec

## 5. Integration check

- [ ] 5.1 Walk through add, edit and delete as one user with a second assigned user, confirming the list updates, the other user receives a notification and Inbox mail each time, and Activity History shows the events; verify full `dotnet test` and `ng test` pass
