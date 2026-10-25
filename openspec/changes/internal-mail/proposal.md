# Proposal

## Why

TaskFlow users want to write to each other inside the application, Gmail-style, instead of going
through an outside mail provider. Deadline reminders should land in that same Inbox. The SMTP
delivery built in `email-notifications` (PR #16, not merged) sends to external Gmail-type addresses,
which the users do not want. This change replaces it with an internal mail system: no message ever
leaves TaskFlow, and every recipient must be an active TaskFlow user.

## What Changes

- **Mail screen** at `/mail` for every signed-in user, Gmail-like:
  - folders: Inbox, Starred, Important, Sent, Drafts and Trash, with unread counts
  - a message list and a reading pane that shows the whole conversation
  - a Compose dialog
  - an unread badge on the "Mail" menu entry
- **Compose and send** to one or more TaskFlow users by their app email address (To and Cc). Any
  address that is not an active TaskFlow user is rejected, with no partial send.
- **Reply, Reply all and Forward**, kept together as one conversation thread.
- **Drafts:** save, edit, send and discard.
- **Trash:** move a mail to Trash, restore it, or delete it forever. Delete forever hides it from that
  user only and keeps the stored record.
- **Starred and Important** flags per user, each with its own folder view.
- **Search** within the user's own mailbox by subject, body text, and sender or recipient name or
  email.
- **Attachments:** upload files, attach them to a draft or mail, and download them. Size, count and
  type limits apply, and only people who can see the mail can download its files.
- **Reminders arrive in the Inbox.** Each generated deadline reminder also becomes an Inbox mail from
  a "TaskFlow" system sender, linked to the task, in the same transaction as the bell notification.
  It is sent at most once per reminder event. A Reminder Settings switch "Also deliver reminders to
  Mail" replaces the old email toggle and is on by default.
- **BREAKING (unreleased work only):** the SMTP email work from `email-notifications` / PR #16 is
  removed:
  - the MailKit package, the SMTP settings, password and screen, and `/api/v1/settings/email`
  - the email dispatcher and send-time logic, and the `EmailDeliveryLogs` table (dropped by a new
    migration)
  - the Gmail address in `appsettings.json`
  - the `email-notifications` change folder, which is superseded

  None of this has been merged into `Develop_env`.
- **BREAKING (API):** the reminder settings field `emailEnabled` is replaced by `mailEnabled`.

## Capabilities

### New Capabilities

- `internal-mail`: users writing to each other inside TaskFlow. Covers the mailbox folders, compose
  and send to app users only, reply, reply all and forward threads, drafts, trash and restore, the
  starred and important flags, read/unread state and unread counts, search, mailbox privacy, and the
  Mail screen.
- `mail-attachments`: uploading, attaching, limiting and downloading files on mails, with access
  restricted to people who can see the mail.
- `reminder-mail`: delivering deadline reminders into recipients' Inboxes from the TaskFlow system
  sender, controlled by a reminder setting, with at most one mail per reminder event.

### Modified Capabilities

None. `openspec/specs/` is still empty: no earlier change has been archived.
- **`deadline-reminders`:** this change keeps every in-app reminder behavior in that unarchived change
  exactly as it is. Its `reminder-settings` "email preference" requirement is replaced in practice by
  the `mailEnabled` switch described in `reminder-mail`. That requirement should be dropped when
  `deadline-reminders` is archived.
- **`email-notifications`:** this change supersedes it. Its folder is deleted rather than archived,
  because it was never released.

## Impact

- **Domain:** new `MailMessage`, `MailRecipient`, `MailboxItem` and `MailAttachment` entities. New
  enums `MailFolder` and `MailRecipientKind`. The `EmailDeliveryLog` entity and the `EmailKind`,
  `EmailDeliveryStatus` and `SmtpSecurity` enums are removed.
- **Infrastructure:**
  - new `MailService`, `MailAttachmentService` and attachment file storage
  - `ReminderService` writes the reminder mail instead of queueing an email
  - the `EmailDispatcher`, `MailKitEmailSender`, `EmailSettingsService` and `EmailDeliveryService`
    are removed
  - a migration `AddInternalMail` creates the mail tables, drops `EmailDeliveryLogs` and deletes the
    `Email.*` and `Notification.EmailEnabled` settings rows
- **API:** a new `MailController` under `/api/v1/mail`. `EmailSettingsController` is removed. The
  reminder settings DTO changes from `emailEnabled` to `mailEnabled`.
- **Configuration:** a new `Mail` section (attachment folder and limits). The `Email` section and the
  MailKit dependency are removed.
- **Frontend:**
  - a new `features/mail` area (folder rail, list, reading pane, compose with recipient picker and
    attachments)
  - `MailService` with the unread count, and a "Mail" nav entry with a badge for all roles
  - the Email Settings screen, service, route and nav entry are removed
  - the Reminder Settings toggle is relabelled
- **Storage:** attachment files go under `database/mail-attachments/` by default. Backups need to
  include that folder.
- **Delivery:** PR #16 (`Email_Notifications_Feature`) is reworked on the same branch and retitled.
