# SCREEN 14: Email Notification Settings Screen

> Source: BRD for Task management.docx · TaskFlow – Intelligent Task Allocation & Tracking System · v1.0 · §2.14

> **STATUS: SUPERSEDED — do not implement.**
> This screen's SMTP configuration was built and then deliberately removed (see
> `openspec/changes/internal-mail/proposal.md`). Outbound email was replaced by the internal Mail
> module (`/mail`); the SMTP settings rows, delivery logs and `EmailSettingsController` were dropped
> in the migration `20261017090431_AddInternalMail`. The only surviving concept is the
> "Also deliver reminders to Mail" toggle, owned by screen 13 (Reminder Settings). Reminder
> delivery failures continue to fall back to in-app notifications, satisfying original FRs 5 and 7
> through the Mail module and `NotificationPublisher` instead of SMTP.

**Screen Name:** Email Configuration
**Access:** Administrator

## Purpose

Configure email delivery for task reminders.

## Screen Elements

- Enable/disable email notifications.
- SMTP server configuration.
- SMTP port.
- Sender email address.
- SMTP username.
- SMTP password or application-specific credential.
- Test email button.
- Save configuration button.

## Functional Requirements

1. Allow authorized administrators to configure email delivery.
2. Read sensitive credentials from environment variables or a secure configuration mechanism.
3. Never display or log SMTP passwords.
4. Provide a test-email function.
5. Send deadline reminders to the appropriate recipients.
6. Record email delivery attempts and failures.
7. Continue generating in-app notifications if email delivery fails.
8. Display an appropriate configuration error if email settings are invalid.

## Expected Result

The application can send deadline reminders by email when email delivery is configured.
