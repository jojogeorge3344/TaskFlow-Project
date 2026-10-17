# Spec Delta

## Purpose

Lets TaskFlow users write to each other inside the application with a Gmail-like mailbox: folders,
conversations, drafts, trash, flags and search. No message ever leaves TaskFlow or reaches someone
who is not a TaskFlow user.

## ADDED Requirements

### Requirement: Mail is available to every signed-in user

Every authenticated user, whatever their role, SHALL have a personal mailbox. The system MUST reject
mail requests from unauthenticated callers.

#### Scenario: Employee opens Mail

- **WHEN** an Employee opens the Mail screen
- **THEN** their own Inbox is shown

#### Scenario: Unauthenticated request

- **WHEN** a mail endpoint is called without a valid session
- **THEN** the request is rejected as unauthorized

### Requirement: Recipients must be active TaskFlow users

A mail SHALL be addressed with To and optional Cc recipients identified by their TaskFlow account
email address. Every recipient MUST be an active, non-deleted TaskFlow user. A send with any other
address, an empty recipient list or more than 50 recipients SHALL be rejected as a whole with a field
error naming the invalid entries, and nothing SHALL be delivered.

#### Scenario: Send to colleagues

- **WHEN** a user sends a mail To one TaskFlow user and Cc another
- **THEN** both receive it in their Inbox and the sender sees it in Sent

#### Scenario: External address rejected

- **WHEN** a user tries to send to "someone@gmail.com", which is not a TaskFlow user
- **THEN** the send is rejected with an error naming that address, and no one receives the mail

#### Scenario: Deactivated user rejected

- **WHEN** a user tries to send to the email address of a deactivated account
- **THEN** the send is rejected and nothing is delivered

#### Scenario: Recipient picker suggests only app users

- **WHEN** a user types part of a name or email in the To field
- **THEN** only matching active TaskFlow users are suggested

### Requirement: Mail content

A mail SHALL have a subject of up to 200 characters and a plain-text body of up to 20,000
characters. A sent mail SHALL record the sender and the time it was sent. A mail with neither a
subject nor a body SHALL NOT be sent. Sent mails are immutable.

#### Scenario: Body too long

- **WHEN** a user sends a body longer than 20,000 characters
- **THEN** the send is rejected with a field error on the body

#### Scenario: Empty mail

- **WHEN** a user sends a mail with an empty subject and an empty body
- **THEN** the send is rejected

### Requirement: Mailbox folders

Each user SHALL have the folders Inbox, Starred, Important, Sent, Drafts and Trash.
- Inbox holds mails received and not trashed.
- Sent holds mails the user sent and did not trash.
- Drafts holds the user's unsent drafts.
- Starred and Important list every non-trashed mail the user flagged, whether received or sent.
- Trash lists the user's trashed mails.

#### Scenario: Folder contents

- **WHEN** a user receives two mails, sends one and stars the sent one
- **THEN** Inbox shows the two received mails, Sent shows the sent one, and Starred shows the sent one

#### Scenario: Sending to oneself

- **WHEN** a user includes their own address as a recipient
- **THEN** the mail appears in both their Sent and their Inbox

### Requirement: Folder lists

Folder lists SHALL be newest first and paginated. Each row SHALL show the sender (or the recipients,
in Sent), the subject, a short preview, the time, the unread state and the star, important and
attachment indicators.

#### Scenario: Newest first with indicators

- **WHEN** a user opens a folder containing an older read mail and a newer unread, starred mail with an attachment
- **THEN** the newer mail is listed first and shows as unread, starred and with an attachment

### Requirement: Read and unread state

A received mail SHALL start unread for each recipient. Opening it SHALL mark it read for that
recipient only. A user SHALL be able to mark a mail unread again. The system SHALL report each user's
unread Inbox count, and the Mail menu entry SHALL show it as a badge that refreshes periodically.

#### Scenario: Read state is per recipient

- **WHEN** a mail is sent to two users and one of them opens it
- **THEN** it is read for that user and still unread for the other

#### Scenario: Unread badge

- **WHEN** a user has three unread Inbox mails
- **THEN** the Mail menu entry shows 3

### Requirement: Mailbox privacy

A user SHALL see only the mails in their own mailbox: those they sent, received or drafted. Reading,
flagging, trashing or replying to a mail that is not in the caller's mailbox MUST be refused exactly
as if the mail did not exist. Administrators get no access to other users' mail.

#### Scenario: Reading someone else's mail

- **WHEN** a user requests a mail that was neither sent to them nor by them
- **THEN** the request returns not found

#### Scenario: Administrator has no special access

- **WHEN** an Administrator requests a mail exchanged only between two other users
- **THEN** the request returns not found

### Requirement: Reply and forward

Reply, Reply all and Forward SHALL create a new mail in the original mail's conversation.
- **Reply** addresses the original sender.
- **Reply all** addresses the sender plus all the original To and Cc recipients, except the replying
  user.
- **Forward** has no preset recipients and carries the original text quoted, plus its attachments.

Subjects SHALL be prefixed "Re: " or "Fwd: " once, not repeatedly.

#### Scenario: Reply all

- **WHEN** a recipient of a mail sent To A and Cc B by S chooses Reply all
- **THEN** the reply is addressed to S and to whichever of A and B is not the replying user, with subject "Re: <original>"

#### Scenario: Prefix not repeated

- **WHEN** a user replies to a mail whose subject already starts with "Re: "
- **THEN** the reply's subject is unchanged

#### Scenario: Forward keeps attachments

- **WHEN** a user forwards a mail with two attachments to a colleague
- **THEN** the colleague receives the quoted text and can download both attachments

### Requirement: Conversation view

Opening a mail SHALL show every mail of its conversation that is in the caller's mailbox, oldest
first. Mails of the conversation outside the caller's mailbox SHALL NOT be shown.

#### Scenario: Thread shown together

- **WHEN** a user opens a reply in their Inbox
- **THEN** the earlier mails of that conversation that are in their mailbox are shown above it

### Requirement: Drafts

A user SHALL be able to save an unsent mail as a draft, with or without valid recipients, edit it,
send it and discard it. Drafts SHALL be visible only to their author and SHALL never be delivered
until sent. Sending a draft applies all send rules and moves it from Drafts to Sent.

#### Scenario: Draft not visible to recipients

- **WHEN** a user saves a draft addressed to a colleague
- **THEN** the colleague receives nothing until the draft is sent

#### Scenario: Send a draft

- **WHEN** the user opens the draft and sends it
- **THEN** it leaves Drafts, appears in Sent, and the colleague receives it

### Requirement: Trash and delete forever

A user SHALL be able to move any mail in their mailbox to Trash, restore it to the folder it came
from, and delete it forever from Trash. These actions SHALL affect only that user's copy. Delete
forever SHALL remove the mail from all of that user's views permanently. Discarding a draft deletes
it.

#### Scenario: Trash affects only me

- **WHEN** a recipient moves a mail to Trash
- **THEN** it leaves their Inbox, appears in their Trash, and is unchanged for the sender and other recipients

#### Scenario: Restore

- **WHEN** the user restores that mail from Trash
- **THEN** it returns to their Inbox with its read and flag state kept

#### Scenario: Delete forever

- **WHEN** the user deletes a mail forever from Trash
- **THEN** it no longer appears in any of their folders or searches

### Requirement: Starred and Important

A user SHALL be able to toggle Starred and Important on any mail in their mailbox. The flags are
personal and do not change for other users.

#### Scenario: Personal flags

- **WHEN** a recipient stars a mail
- **THEN** it appears in their Starred folder and does not appear in the sender's Starred folder

### Requirement: Search

A user SHALL be able to search their own mailbox, excluding Trash unless searching inside Trash, by
text matched case-insensitively against the subject, the body and the sender and recipient names and
emails. Results SHALL be newest first and limited to the current folder when a folder is selected.

#### Scenario: Search by sender

- **WHEN** a user searches for "priya"
- **THEN** mails in their mailbox sent by or addressed to Priya are listed, and no mail outside their mailbox is

#### Scenario: Search within a folder

- **WHEN** a user searches for "report" while in Sent
- **THEN** only their sent mails matching "report" are listed

### Requirement: Mail screen

The Mail screen SHALL show:
- a folder rail with unread counts
- a message list with selection, a search box and pagination
- a reading pane with the conversation and the Reply, Reply all, Forward, Star, Important, Mark
  unread and Trash actions
- a Compose dialog with To and Cc recipient pickers, subject, body, attachments, Send, Save draft and
  Discard

On narrow screens it SHALL show one pane at a time and work at phone width without horizontal
scrolling.

#### Scenario: Compose and send from the screen

- **WHEN** a user opens Compose, picks a recipient, writes a subject and body and presses Send
- **THEN** the dialog closes, a confirmation is shown and the mail appears in Sent

#### Scenario: Phone width

- **WHEN** the Mail screen is used at 390px wide
- **THEN** the folder list, message list and reading pane are shown one at a time with a way back, and nothing scrolls horizontally
