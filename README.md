# TaskFlow

## Mail

TaskFlow has its own internal mail, like Gmail but only between TaskFlow users. Open **Mail** in the side menu,
available to every role.

- **Folders:** Inbox, Starred, Important, Sent, Drafts and Trash, with unread counts. The Mail menu entry shows how
  many Inbox mails are unread.
- **Compose:** write to one or more colleagues in **To** and **Cc**. Recipients are picked from TaskFlow users by name
  or email. An address that is not an active TaskFlow user (for example a Gmail address) is rejected, and nothing
  ever leaves TaskFlow.
- **Reply, Reply all and Forward** keep mails together as one conversation. Forwarding includes the original
  attachments.
- **Drafts:** save, edit and send later, or discard.
- **Trash:** restore mail, or delete it forever. These actions affect only your own copy.
- **Search:** search your own mailbox by subject, text, sender or recipient.
- **Privacy:** each person sees only their own mail. Administrators have no access to other people's mail.

### Attachments

| Limit | Value |
|---|---|
| Per file | 10 MB |
| Per mail | 5 files, 25 MB in total |
| Blocked types | `.exe .dll .bat .cmd .com .msi .ps1 .sh .js .vbs .scr` |

Files are stored under generated names in the folder set by `Mail:AttachmentsPath` in `appsettings.json`. The
default is `../database/mail-attachments`, resolved from the backend folder like the database, so it sits next to
`database/taskflow.db`. Only the uploader and people who have the mail in their mailbox can download a file.
**Back up this folder together with the database.**

### Deadline reminders in Mail

Every automatic deadline reminder also arrives in the recipient's Inbox as a mail from **TaskFlow**, with a link to
the task. These mails cannot be replied to, but they can be forwarded. To turn this off, clear
**Reminder Settings → Also deliver reminders to Mail**. Bell notifications are always sent either way.

`Mail:AppBaseUrl` sets the address used for the task link (default `http://localhost:4200`).
