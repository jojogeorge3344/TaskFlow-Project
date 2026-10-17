# Design

## Context

See `proposal.md` for the motivation and the three specs for the required behavior. The current
state that shapes the approach:

- **Branch state.** PR #16 (`Email_Notifications_Feature`, not merged) added SMTP delivery:
  - `EmailDeliveryLog` and its migration `AddEmailDelivery`
  - `EmailDispatcher` and `MailKitEmailSender`
  - `EmailSettingsService`, `EmailDeliveryService` and `EmailSettingsController`
  - the `Email.*` settings keys and seeding from the `Email` configuration section
  - the Email Settings screen

  **The developer's local `database/taskflow.db` already has `AddEmailDelivery` applied**: it is in
  the WAL from a Visual Studio run. The removal must therefore be a forward migration, not deleting
  that migration.
- **Reminder engine.** In `ReminderService.TryDeliverAsync` there is one transaction per (task,
  recipient, reminder) that:
  1. runs an insert-or-ignore into `ReminderHistory` on the unique key
  2. reads the new history `Id` by that key
  3. adds the `Notification`, and currently the queued `EmailDeliveryLog`
  4. commits

  Recipients come from `LoadRecipientsAsync` (active users with `FullName` and `Email`).
- **Users.** `User` has `Email`, `FullName`, `Role`, `IsActive` and `IsDeleted` (global query filter
  `!IsDeleted`). `GET /api/v1/users` is Manager/Admin only, so Employees cannot list users today.
- **Patterns to follow.**
  - Thin controllers with FluentValidation and `ValidationProblemDetails`.
  - List endpoints return `{ items, total, page, pageSize }`.
  - The caller id comes from `ClaimTypes.NameIdentifier`.
  - The `INotificationService` unread-count endpoint, polled by the shell on the
    session-check interval, drives the bell badge.
  - Standalone Angular components, root services in `core/services`, the `.tf-*` UI primitives, the
    `DrawerComponent` / `ConfirmDialogComponent` in `shared/ui`, and `ToastService`.
- **Not in the codebase yet:** file upload or download. `DatabasePathResolver` resolves the SQLite
  path relative to the content root.

## Goals / Non-Goals

**Goals:**
- **Per-user mailbox rows**, so read, star, important and trash are personal and cheap to query, and
  privacy is a single `OwnerUserId = caller` filter.
- **Reminder mails in the existing reminder transaction**, so the reminder history unique key keeps
  guaranteeing at most one mail per event with no new deduplication logic.
- **Remove all SMTP code and configuration** so nothing can send outside TaskFlow.
- **Attachment storage that is safe by construction:** generated names, outside the web root, and
  always served as a download.

**Non-Goals:**
- Real-time push. The unread badge polls on the existing shell interval.
- Bcc, labels or custom folders, rich text or HTML bodies, read receipts, mail rules, scheduled send,
  and undo send.
- Bell notifications for new user-to-user mail. Mail has its own badge.
- Purging attachment files from disk when a mail is deleted forever, and cleaning up orphan uploads.
  Records and files are kept; see Risks.
- Administrator access to other users' mail.

## Decisions

**D1 - Data model.** The model has four tables.

**`MailMessage`**, the content, written once when sent:

| Column | Notes |
|---|---|
| `Id` | |
| `ConversationId` | `Guid`: a new value for a new mail, copied from the parent for reply or forward |
| `ParentMessageId?` | |
| `SenderUserId?` | Null means the TaskFlow system sender |
| `IsSystem` | |
| `Subject` | Max 200 |
| `Body` | Max 20,000 |
| `TaskId?` | |
| `ReminderHistoryId?` | Unique filtered index: one reminder mail per event (D4) |
| `DraftTo` / `DraftCc` | Text; raw addresses kept only while it is a draft |
| `CreatedAt` | |
| `UpdatedAt` | |
| `SentAt?` | Null means it is still a draft |

**`MailRecipient`**: `Id`, `MessageId`, `UserId`, `Kind` (`To` or `Cc`). Unique on
`(MessageId, UserId)`. These are the resolved recipients of a sent mail.

**`MailboxItem`**, one user's copy of a mail:

| Column | Notes |
|---|---|
| `Id` | |
| `MessageId` | |
| `OwnerUserId` | |
| `Folder` | `MailFolder { Inbox, Sent, Drafts }` |
| `IsRead`, `IsStarred`, `IsImportant` | Personal flags |
| `TrashedAt?` | |
| `PurgedAt?` | |
| `SortAt` | `SentAt`, or `UpdatedAt` for drafts |

There is a unique index on `(MessageId, OwnerUserId, Folder)`, so mailing yourself gives both a Sent
and an Inbox item, and an index on `(OwnerUserId, Folder, PurgedAt, TrashedAt, SortAt)`.

The folders the user sees are queries over that user's items, all excluding `PurgedAt != null`:

| Folder | Query |
|---|---|
| Inbox | `Folder = Inbox` and not trashed |
| Sent | `Folder = Sent` and not trashed |
| Drafts | `Folder = Drafts` |
| Starred | `IsStarred` and not trashed and `Folder != Drafts` |
| Important | `IsImportant` and not trashed and `Folder != Drafts` |
| Trash | `TrashedAt != null` |

Restoring an item clears `TrashedAt`. The item keeps its own `Folder`, so it returns where it came
from. Delete forever sets `PurgedAt`, and the rows are kept.

**`MailAttachment`**:

| Column | Notes |
|---|---|
| `Id` | |
| `MessageId?` | Null while it is only uploaded |
| `UploadedByUserId` | |
| `FileName` | Sanitized display name |
| `ContentType` | |
| `SizeBytes` | |
| `StorageKey` | `Guid`, the file name on disk |
| `CreatedAt` | |

Forwarding creates new attachment rows that point to the same `StorageKey`. Files are immutable, so
sharing them is safe.

Alternatives considered:
- A single message table with per-recipient JSON flags. This cannot be indexed for the folder queries.
- Gmail-style conversation rows in the list. Grouping complicates paging and unread counts; the list
  shows individual mails, and the reading pane shows the conversation.

**D2 - Send is one transaction.** `MailService.SendAsync(callerId, request)`:
1. Normalizes To and Cc, trimming, lowercasing and de-duplicating them. A user listed in both To and
   Cc keeps To.
2. Resolves the addresses with one query on `Users` where `Email` is in the set and the user is active
   and not deleted (the query filter excludes deleted users). Any unresolved address produces a 400
   that names the addresses, and nothing is written.
3. Validates the attachment ids: each must be uploaded by the caller and either unattached or attached
   to the draft being sent. The validator enforces the count, the 25 MB total and the 5-file limit.
4. Inside one transaction:
   - creates or updates the message, setting `SentAt`
   - adds the `MailRecipient` rows
   - gives the sender a `Sent` item (read)
   - gives each recipient an `Inbox` item (unread)
   - attaches the files
   - deletes the draft's `Drafts` item and clears `DraftTo` and `DraftCc` if it was a draft

FluentValidation covers the request shape: subject ≤200, body ≤20,000, not both empty, 1–50 recipients
in total, and well-formed address strings. Resolution to users happens in the service.

**D3 - Reply, Reply all and Forward.**
- `GET /mail/items/{itemId}/compose?mode=reply|replyAll|forward` returns the prefilled recipients,
  subject, quoted body and forwarded attachments (for forward). The UI therefore never re-implements
  the rules.
- The send request carries `ParentItemId` and `Mode`. The server re-checks that the parent item
  belongs to the caller, then copies `ConversationId` and sets `ParentMessageId`.
- Reply and Reply all on an `IsSystem` parent return 400.
- The prefix rule is: add "Re: " or "Fwd: " unless the subject already starts with that prefix,
  ignoring case.
- For Forward, the quoted body is a header block ("---------- Forwarded message ----------", From,
  Date, Subject, To) followed by the original body.
- The thread view returns the messages with the same `ConversationId` for which the caller has a
  non-purged item that is not a draft. They are ordered by `SentAt` ascending, and each is
  de-duplicated to one entry even when the caller has both a Sent and an Inbox item for it.

**D4 - Reminder mail in the reminder transaction; the dispatcher is removed.** In
`ReminderService.TryDeliverAsync` the `EmailDeliveryLog` block is replaced. When
`Notification.MailEnabled` is true, the service adds:
- a `MailMessage` with `IsSystem`, `SenderUserId = null`, `TaskId`, `ReminderHistoryId`, a subject
  ("Task reminder: <title>", "Task due today: <title>" or "Overdue task: <title>") and the existing
  body text plus the task link (`Mail:AppBaseUrl`)
- a `To` `MailRecipient` for the recipient
- an unread `Inbox` `MailboxItem`

These are written in the same transaction as the history row and the notification.

The history insert-or-ignore stays the deduplication gate, and the unique `ReminderHistoryId` index is
the backstop. Mail lands at generation time (local midnight), just like the bell notification. The
SMTP-specific pieces have no purpose for internal mail and are deleted:
- the send time
- the retries
- the outbox
- the claim lease
- the dispatcher

`ReminderSchedulerService` returns to a single step.

Alternative considered: keep the outbox and dispatcher to deliver at 09:00. It was rejected because
internal delivery cannot fail transiently, and a second pipeline would only add states, such as
pending, skipped and expired, that users would never see.

**D5 - Settings.**
- `SettingKeys` drops `Notification.EmailEnabled` and the seven `Email.*` keys, and adds
  `Notification.MailEnabled` (default `true`).
- `ReminderSettingsDto` and `UpdateReminderSettingsRequest` replace `EmailEnabled` with
  `MailEnabled`, and the validator requires it.
- `SettingsInitializer` inserts the new key.
- The `EmailOptions` SMTP fields go. A `MailOptions` (`Mail` section) holds `AttachmentsPath`
  (default `../database/mail-attachments`, resolved against the content root like the database
  path) and `AppBaseUrl`.

**D6 - Removing the SMTP work.** These are deleted:
- in Infrastructure and Application: `EmailDeliveryLog`, `EmailKind`, `EmailDeliveryStatus`,
  `SmtpSecurity`, `EmailDispatcher`, `MailKitEmailSender`, `IEmailSender`, `IEmailDispatcher`,
  `IEmailDeliveryService`, `EmailDeliveryService`, `IEmailSettingsService`, `EmailSettingsService`,
  `EmailSettingsDtos`, `EmailConfigurationCheck` and both email validators
- in the API: `EmailSettingsController`
- in the projects: the MailKit package
- in configuration: the `Email` section of `appsettings.json`
- in the tests: the related tests and the `FakeEmailSender`
- in the frontend: the email settings component, service, models, route, nav entry and the Reminder
  Settings link
- in OpenSpec: the `openspec/changes/email-notifications/` folder

`ReminderStatus`'s comment is updated. The `README` email section is replaced by a short Mail
section.

**D7 - Migration `AddInternalMail`.** The migration is forward-only and works whether or not
`AddEmailDelivery` has run. It:
- creates the four mail tables
- drops `EmailDeliveryLogs` (EF `DropTable`; `AddEmailDelivery` exists in every database that reaches
  this point, because it precedes this migration)
- runs `DELETE FROM "ApplicationSettings" WHERE "Key" LIKE 'Email.%' OR "Key" = 'Notification.EmailEnabled'`

`SettingsInitializer` then adds `Notification.MailEnabled`.

**D8 - Attachments.** `IMailFileStore` (with `LocalMailFileStore`) provides
`SaveAsync(stream) → storageKey`, `OpenRead(storageKey)` and `Exists`. Each file is written to
`<AttachmentsPath>/<first 2 chars>/<guid>`, with the directory created when needed.

Upload is `POST /mail/attachments`, a single `IFormFile`, with `[RequestSizeLimit(11 MB)]`. The server
checks:
- size: 1 byte to 10 MB
- extension: the blocklist from the spec
- name: reduced to `Path.GetFileName` with invalid characters stripped, at most 255 characters, and
  "file" when empty

The content is streamed to disk, and the response returns `{ id, fileName, sizeBytes, contentType }`.

Download is `GET /mail/attachments/{id}`. It is allowed when the caller uploaded the file, or when the
caller has a non-purged item for the attachment's message. Otherwise it returns 404. The response is
`File(stream, contentType, fileName)`, which sets `Content-Disposition: attachment`, plus
`X-Content-Type-Options: nosniff`. The content type is taken from a small allowlist (image, PDF,
Office, text and zip types) and is otherwise `application/octet-stream`.

`DELETE /mail/attachments/{id}` removes an unattached upload that belongs to the caller, record only.

**D9 - API surface** (`MailController`, `[Authorize]`, all scoped to the caller):

| Method and path | Purpose |
|---|---|
| `GET /mail/summary` | Per-folder counts: Inbox unread, Drafts total, Starred and Important unread |
| `GET /mail/unread-count` | `{ count }` for the badge |
| `GET /mail?folder=&search=&page=&pageSize=` | Folder list, max 50 per page |
| `GET /mail/items/{itemId}` | Message, attachments and thread; marks the item read |
| `PATCH /mail/items/{itemId}` | `{ isRead?, isStarred?, isImportant? }` |
| `POST /mail/items/{itemId}/trash` | Move to Trash |
| `POST /mail/items/{itemId}/restore` | Restore from Trash |
| `DELETE /mail/items/{itemId}` | Delete forever (Trash only; 409 otherwise); discards a draft |
| `GET /mail/items/{itemId}/compose?mode=` | Prefilled reply, reply-all or forward |
| `POST /mail/send` | Send a new mail, a reply, a forward or a draft (`draftId`) |
| `POST /mail/drafts` | Save a new draft |
| `PUT /mail/drafts/{messageId}` | Update a draft |
| `GET /mail/contacts?search=` | Up to 10 active users `{ id, fullName, email }`; empty search returns nothing |
| `POST /mail/attachments`, `GET /mail/attachments/{id}`, `DELETE /mail/attachments/{id}` | Attachments (D8) |

Items that are not the caller's always return 404 (spec "Mailbox privacy").

Search uses `EF.Functions.Like` with `%term%` (SQLite `LIKE` is case-insensitive for ASCII) over the
subject, the body, the sender's name and email and the recipients' names and emails. Wildcard
characters in the term are escaped.

**D10 - Frontend.**
- **`core/services/mail.service.ts`** holds all API calls and an `unreadCount` signal. The shell
  refreshes it on the same interval as the notification badge. The models live in
  `core/models/mail.model.ts`.
- **`features/mail/`**:
  - `mail.component`: the three-pane layout. Folder and item come from the query parameters
    (`/mail?folder=inbox&item=42`), so back and forward work.
  - `mail-list.component`: rows, the search box, pagination and the per-row star toggle.
  - `mail-reader.component`: the thread, attachment chips and the action bar. Reply actions are
    hidden for system mail.
  - `compose-dialog.component`: built on `DrawerComponent`, full screen on phones. It has
    `recipient-picker.component` chips (debounced contacts search; free text that isn't a TaskFlow
    user shows an inline error before sending), subject and body, attachment upload with a progress
    and remove control, and Send, Save draft and Discard.
- **Phone layout:** below 768px one pane is shown at a time, with a back button.
- **Navigation:** "Mail" with the `mail` icon and the unread badge, for all roles, after Dashboard.
- **Reminder Settings:** the toggle is relabelled "Also deliver reminders to Mail" and bound to
  `mailEnabled`.

## Risks / Trade-offs

- **[Risk] Disk growth from attachments.** Delete forever keeps the files. → There is a 10 MB / 25 MB
  cap per mail. Purging and orphan cleanup are noted as follow-ups. The README backup note includes
  the attachment folder.
- **[Risk] A malicious upload is served back.** → Uploads are always served with
  `Content-Disposition: attachment`, `nosniff` and a content-type allowlist. Executable types are
  blocked. Files are stored outside `wwwroot` under generated names, so names cannot traverse paths.
- **[Risk] SQLite `LIKE` search is slow over large bodies.** → Office scale is thousands of mails. The
  query is first narrowed by the owner index and then pages. Full-text search can come later.
- **[Trade-off] Reminder mails arrive at local midnight, not 09:00.** This matches the bell
  notification. The SMTP send time is removed (D4).
- **[Trade-off] The API changes from `emailEnabled` to `mailEnabled`.** Only the in-repo frontend
  consumes it, and both change in the same PR.
- **[Risk] Existing local databases keep the orphaned `Email.*` rows or `EmailDeliveryLogs`.** → D7
  removes both in the migration.
- **[Trade-off] The user directory is exposed through `/mail/contacts`.** It returns name and email
  for active users matching a search, capped at 10, to any signed-in user. Colleagues need this to
  address mail, and it reveals nothing beyond what an internal address book would.

## Migration Plan

1. On the `Email_Notifications_Feature` branch, remove the SMTP code (D6) and add the mail
   implementation and the `AddInternalMail` migration (D7).
2. On startup `MigrateAsync` applies it. It creates the mail tables, drops `EmailDeliveryLogs` and
   cleans the settings. `SettingsInitializer` adds `Notification.MailEnabled = true`.
3. The attachment folder is created on the first upload.
4. Retitle and redescribe PR #16 for internal mail.
5. Rollback: switch `mailEnabled` off to stop reminder mails. Reverting the migration drops the mail
   tables. The SMTP pieces are not restored, by intent.
