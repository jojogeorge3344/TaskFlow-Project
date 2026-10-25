# Spec Delta

## Purpose

Lets users attach files to internal mails and download them safely. Size, count and type limits
apply, and only people who can see a mail can reach its files.

## ADDED Requirements

### Requirement: Upload and attach files

A user SHALL be able to upload a file and attach it to their draft or to a mail they are composing,
replying or forwarding. An uploaded file that is not attached to a sent mail SHALL be visible only to
the user who uploaded it. Removing an attachment from a draft SHALL detach it.

#### Scenario: Attach and send

- **WHEN** a user uploads "report.pdf", attaches it and sends the mail
- **THEN** every recipient sees "report.pdf" with its size on the mail and can download it

### Requirement: Attachment limits

The system SHALL reject, with a clear message:
- a file larger than 10 MB
- more than 5 attachments on one mail
- attachments totalling more than 25 MB on one mail
- an empty file
- a blocked executable or script type: `.exe`, `.dll`, `.bat`, `.cmd`, `.com`, `.msi`, `.ps1`,
  `.sh`, `.js`, `.vbs` or `.scr`

#### Scenario: Oversized file

- **WHEN** a user uploads a 12 MB file
- **THEN** the upload is rejected with a message stating the 10 MB limit and nothing is stored

#### Scenario: Blocked type

- **WHEN** a user uploads "setup.exe"
- **THEN** the upload is rejected because the file type is not allowed

#### Scenario: Too many attachments

- **WHEN** a user tries to send a mail with six attachments
- **THEN** the send is rejected and nothing is delivered

### Requirement: Attachment access control

A user SHALL be able to download an attachment only when the mail carrying it is in their mailbox, or
when they uploaded it. Any other request MUST be refused as not found.

#### Scenario: Recipient downloads

- **WHEN** a recipient downloads an attachment of a mail in their Inbox
- **THEN** they receive the file content with its original name

#### Scenario: Outsider is refused

- **WHEN** a user who is neither the uploader nor in the mail's mailbox requests the attachment
- **THEN** the request returns not found

### Requirement: Safe storage and download

The system SHALL store attachment content outside the web root under a server-generated name, never
the user-supplied file name. Downloads SHALL be sent as attachments with the original file name and
a safe content type, so that the browser never runs them inline as active content. File content
SHALL survive application restarts.

#### Scenario: Path tricks are harmless

- **WHEN** a user uploads a file named "../../appsettings.json"
- **THEN** it is stored under a generated name in the attachment folder, and downloading returns it as a file named "appsettings.json" without touching any other file
