# SCREEN 14: Email Notification Settings Screen

> Source: BRD for Task management.docx · TaskFlow – Intelligent Task Allocation & Tracking System · v1.0 · §2.14

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
