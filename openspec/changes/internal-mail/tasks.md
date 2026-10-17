# Tasks

## 1. Remove the SMTP email work

- [x] 1.1 Delete the backend SMTP pieces listed in design D6:
  - the entity and enums, the dispatcher, the sender, the email settings and delivery services, the
    DTOs, the configuration check, both validators and `EmailSettingsController`
  - their DI registrations in `Program.cs` and the `Email` section of `appsettings.json`
  - the MailKit package reference
  - the related unit and integration tests and `FakeEmailSender`

  Restore `ReminderSchedulerService.RunPassAsync` to the reminder step only, keeping it public and
  its test, and remove the email-queueing code from `ReminderService` and its tests. Keep the
  `AddEmailDelivery` migration file, because existing databases have applied it. Verify that
  `dotnet build` succeeds and `dotnet test` passes, and that `grep -ri "mailkit\|smtp" src` finds
  nothing.
- [x] 1.2 Delete the frontend Email Settings pieces:
  - the component and its spec, the service and its spec, and the email models
  - the `settings/email` route and its spec entries, and the nav entry with its shell spec
    expectation
  - the "Configure email delivery" link and its specs on Reminder Settings

  Keep the `mail` icon for the new screen. Verify that `npm run build` passes and
  `npx ng test --watch=false --browsers=ChromeHeadless` is green.
- [x] 1.3 Delete `openspec/changes/email-notifications/`, which this change supersedes, and replace
  the README "Email notifications" section with a placeholder heading for Mail (filled in by 9.1).
  Verify that `openspec list` no longer shows `email-notifications` and that
  `openspec validate internal-mail --strict` passes.

## 2. Data model, migration and settings

- [x] 2.1 Add the domain types from D1:
  - the enums `MailFolder` (Inbox, Sent, Drafts) and `MailRecipientKind` (To, Cc)
  - the entities `MailMessage`, `MailRecipient`, `MailboxItem` and `MailAttachment`

  Map them in `AppDbContext`:
  - string enum conversions and the length limits
  - the unique indexes on `(MessageId, UserId)`, `(MessageId, OwnerUserId, Folder)` and the filtered
    `ReminderHistoryId`
  - the owner folder index from D1
  - `Restrict` foreign keys

  Verify `dotnet build`.
- [x] 2.2 Add the migration `AddInternalMail` (D7), which creates the mail tables, drops
  `EmailDeliveryLogs` and deletes the `Email.%` and `Notification.EmailEnabled` settings. Verify it
  applies cleanly to a copy of `database/taskflow.db` (which has `AddEmailDelivery` applied) and to a
  fresh database, and that `dotnet ef migrations has-pending-model-changes` reports none.
- [x] 2.3 Replace the email settings key with `Notification.MailEnabled` (default true) (D5):
  - `SettingKeys` and `SettingsInitializer`
  - the reminder settings DTO, request, validator and service, which change from `EmailEnabled` to
    `MailEnabled`
  - `MailOptions` (`AttachmentsPath`, `AppBaseUrl`) bound in `Program.cs` and set in
    `appsettings.json`

  On the frontend, rename the field in `ReminderSettings`, relabel the toggle "Also deliver reminders
  to Mail" and update its specs. Verify the settings unit tests, `SettingsEndpointsTests`
  (`mailEnabled` defaults to true and round-trips) and the reminder-settings component spec.

## 3. Mail service: sending, folders and flags

- [x] 3.1 Add the request and response DTOs and the FluentValidation validators for send and draft
  (D2 limits). Implement `MailService.SendAsync` (D2): normalize and resolve recipients against
  active users with an all-or-nothing rejection, then in one transaction write the message,
  recipients, Sent item and Inbox items, and mark Inbox items unread. Verify with unit tests for each
  `internal-mail` scenario under "Recipients must be active TaskFlow users" and "Mail content", plus
  sending to oneself and a To/Cc duplicate.
- [x] 3.2 Implement the folder listing (D1 folder rules, newest first, paging, preview, indicators),
  the summary counts, the unread count, opening an item (marks it read and returns the thread, D3),
  the PATCH flags, trash, restore and delete forever, and search (D9 `LIKE` with escaping, folder
  scoped, Trash only inside Trash). Verify with unit tests covering "Mailbox folders", "Folder lists", "Read and
  unread state", "Mailbox privacy" (another user's and an Administrator's access return not found),
  "Trash and delete forever", "Starred and Important" and "Search".
- [x] 3.3 Implement `GetContactsAsync(search)`: up to 10 active, non-deleted users matching the name
  or email, and none for an empty search. Verify with unit tests (deactivated users excluded, cap of
  10).

## 4. Drafts and conversations

- [x] 4.1 Implement drafts: create and update (raw `DraftTo`/`DraftCc` allowed to be invalid), send
  a draft (all send rules, then moved to Sent), and discard (delete forever on a draft). Verify with
  unit tests for "Drafts" (not visible to recipients, sending moves it to Sent, the author only).
- [x] 4.2 Implement the compose context and threaded send (D3): reply, reply all and forward
  recipients, the one-time "Re: " / "Fwd: " prefix, the forward quote block and attachment copies,
  `ConversationId` and `ParentMessageId`, a parent-ownership check, and refusal to reply to system
  mail. Verify with unit tests for every "Reply and forward" and "Conversation view" scenario and the "System mails cannot be
  replied to" scenario.

## 5. Attachments

- [x] 5.1 Add `IMailFileStore` with `LocalMailFileStore` (D8): generated keys, sharded folders,
  streaming writes, and a path resolved from `MailOptions` against the content root. Implement
  upload validation (size, empty file, blocklist, name sanitizing), download authorization
  (uploader, or a non-purged mailbox item) and unattached delete. Wire the attachments into send and
  drafts (ownership, at most 5, at most 25 MB in total). Verify with unit tests for every
  `mail-attachments` scenario, including "../../appsettings.json" stored under a generated name in a
  temp folder.

## 6. Reminder mail

- [x] 6.1 In `ReminderService.TryDeliverAsync`, when `MailEnabled` is on, write the system
  `MailMessage` (subject by reminder kind, body with deadline and task link), the To recipient and
  the unread Inbox item in the existing transaction (D4). Verify by extending `ReminderServiceTests`
  and `ReminderIdempotencyTests`:
  - each recipient gets one unread mail from TaskFlow
  - switched off means bell notifications only
  - repeated and concurrent passes create exactly one mail per event
  - a completed task creates none

## 7. Mail API

- [x] 7.1 Add `MailController` with the D9 endpoints (`[Authorize]`, caller-scoped, 404 for foreign
  items, `ValidationProblemDetails` for invalid input, `RequestSizeLimit` on upload, download headers
  from D8). Verify with integration tests:
  - Employee to Manager send and both folders
  - external address 400 with nothing delivered
  - a foreign item is 404 for another user and for an Administrator
  - unauthenticated is 401
  - reply-all recipients
  - draft lifecycle
  - trash, restore and delete forever
  - star and search
  - attachment upload, download with `Content-Disposition: attachment`, a blocked `.exe` and an
    outsider 404
  - contacts available to Employees
  - unread count

## 8. Mail screen

- [x] 8.1 Add `core/models/mail.model.ts` and `core/services/mail.service.ts`, with every D9 call and
  the `unreadCount` signal. Refresh the count from the shell on the notification interval. Add a
  "Mail" nav entry with the `mail` icon and an unread badge for all roles, and the `/mail` route for
  every signed-in user. Verify with the service spec (URLs, methods, bodies, multipart upload) and
  the shell and routes specs (Mail entry and badge for each role).
- [x] 8.2 Build `features/mail/mail.component` (folder rail with counts, query-param state),
  `mail-list.component` (rows, search, paging, star toggle, empty states) and
  `mail-reader.component` (thread, attachment downloads, actions; reply hidden for system mail).
  Verify with component specs (folder switching, unread styling, opening marks read, star toggle,
  trash and restore, no reply on system mail) and `npm run build`.
- [x] 8.3 Build `compose-dialog.component` with `recipient-picker.component` (debounced contacts,
  chips, an inline error for unknown addresses), subject and body, attachment upload and remove with
  limit messages, and Send, Save draft and Discard. Open it for new mail, reply, reply all, forward
  (prefilled from the compose endpoint) and drafts. Verify with component specs (unknown address
  blocks send, the prefill per mode, the upload limit message, save draft, server field errors), then
  `npm run build` and `npx ng test --watch=false --browsers=ChromeHeadless`.
- [x] 8.4 Make the screen responsive (D10): one pane at a time with back below 768px, compose full
  screen on phones, no horizontal scroll at 390px. Verify with a component spec for pane switching
  in narrow mode.

## 9. Documentation and end-to-end check

- [x] 9.1 Write the README "Mail" section: what it does, recipients limited to app users, the
  attachment limits and the `Mail:AttachmentsPath` folder (include it in backups), and the "Also
  deliver reminders to Mail" setting. Verify that the documented folder matches the configured
  default.
- [ ] 9.2 End-to-end check against a running API on a fresh seeded database:
  - an Employee mails a Manager with an attachment, and the Manager replies all
  - star, trash, restore and search work
  - an external address is rejected
  - a reminder arrives in the Inbox once across several scheduler ticks

  Then run a manual browser check as each role, including 390px width. Verify by recording the API
  results and the manual check outcome.

## Workflow follow-up

- Retitle and redescribe PR #16 (`Email_Notifications_Feature`) for internal mail after
  implementation, and review it again.
- Archive `internal-mail` after merge. When `deadline-reminders` is archived, drop its "Email
  preference is recorded only" requirement, because `reminder-mail` replaces it.
