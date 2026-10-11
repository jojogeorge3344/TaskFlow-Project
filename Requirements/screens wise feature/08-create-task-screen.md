# SCREEN 8: Create Task Screen

> Source: BRD for Task management.docx · TaskFlow – Intelligent Task Allocation & Tracking System · v1.0 · §2.8

**Screen Name:** Create New Task
**Access:** Administrator and Manager

## Purpose

Allow managers to create tasks and assign them to employees.

## Screen Elements

- Task title.
- Task description.
- Assigned employee.
- Priority.
- Start date and time.
- Deadline date and time.
- Estimated duration.
- Initial status.
- Additional notification recipients.
- Reminder settings.
- Create task button.
- Cancel button.

## Functional Requirements

1. Generate a unique task ID automatically.
2. Require a task title, assigned employee, and deadline.
3. Allow the manager to enter a description.
4. Allow the manager to set Low, Medium, High, or Critical priority.
5. Allow the manager to specify the task's start date and deadline.
6. Allow the manager to enter an estimated duration in hours.
7. Automatically include the assigned employee in the notification recipients.
8. Allow the task creator or authorized manager to select additional notification recipients.
9. Allow the manager to configure reminder intervals using predefined options.
10. Validate that the deadline is later than the start date and time.
11. Validate that the estimated duration is a positive value.
12. Save the task and create an initial activity-history record.
13. Notify the assigned employee that a new task has been assigned.
14. Record the task creator and creation timestamp.

## Expected Result

A new task is created and becomes visible to authorized users.
