# SCREEN 1: Login Screen

> Source: BRD for Task management.docx · TaskFlow – Intelligent Task Allocation & Tracking System · v1.0 · §2.1

**Screen Name:** User Login
**Access:** All registered users

## Purpose

Allow authorized users to securely access TaskFlow.

## Screen Elements

- Application logo and name.
- Email or username field.
- Password field.
- Show/hide password button.
- Login button.
- Error message area.

## Functional Requirements

1. Users must enter their registered email address or username and password.
2. The system must validate the entered credentials.
3. Successful authentication must redirect users to the appropriate dashboard based on their role.
4. Invalid credentials must display an appropriate error message.
5. Inactive accounts must not be allowed to log in.
6. Passwords must be stored using secure password hashing.
7. User sessions must be handled securely.

## Expected Result

The user enters the application and sees the dashboard associated with their role.
