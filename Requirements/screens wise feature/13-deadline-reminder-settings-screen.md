# SCREEN 13: Deadline Reminder Settings Screen

> Source: BRD for Task management.docx · TaskFlow – Intelligent Task Allocation & Tracking System · v1.0 · §2.13

**Screen Name:** Reminder Settings
**Access:** Administrator or authorized Manager

## Purpose

Allow authorized users to configure automatic deadline reminders.

## Screen Elements

- Enable/disable reminders.
- First reminder interval.
- Second reminder interval.
- Deadline-day reminder.
- Overdue reminder frequency.
- Email notification setting.
- Save settings button.

## Functional Requirements

1. Allow authorized users to configure predefined reminder intervals.
2. Provide default reminders two days before the deadline, one day before the deadline, and on the deadline day.
3. Allow overdue reminders once per day by default.
4. Allow reminder settings to be changed without editing source code.
5. Prevent reminders from being generated for completed or cancelled tasks.
6. Avoid scheduling reminders for times that have already passed.
7. Use a consistent application timezone.
8. Maintain reminder history to prevent duplicate reminders.
9. Display the current reminder configuration.

## Expected Result

Managers can configure how frequently the system reminds employees about deadlines.
