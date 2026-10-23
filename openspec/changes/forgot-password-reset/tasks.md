# Tasks

## 1. Data model

- [x] 1.1 Add password-reset tokens table (userId, tokenHash, expiresAt, used, consumedAt) via an EF Core migration and verify the migration applies cleanly to a clean database
- [x] 1.2 Add the password-reset token entity and repository/query methods and verify a token can be inserted, read by token, and marked used

## 2. Token service (single-use, 5-minute expiry)

- [x] 2.1 Implement crypto-random token generation, hashing at rest, and storage with expiresAt = createdAt + 5 minutes and verify a generated token is stored as a hash, never plaintext
- [x] 2.2 Implement token validation that rejects expired tokens as "expired" before the used check and verify a token older than 5 minutes is rejected
- [x] 2.3 Implement single-use enforcement that marks a token used on consumption and rejects any reuse with "link not valid" and verify a second use of the same token is rejected

## 3. Reset-request flow (user-initiated from the profile screen)

- [x] 3.1 (Superseded) The login screen link now opens the SMS OTP flow from `sms-otp-password-reset`; verify the login link no longer targets the admin-mediated flow
- [x] 3.2 Add a Forgot password link to the profile screen and verify the link renders on the profile screen
- [x] 3.3 Implement the reset-request endpoint that sends a one-time, time-limited reset link to the administrator via internal-mail and verify an email is sent to the administrator containing the link
- [x] 3.4 Add unit tests for the reset-request flow and verify the tests pass

## 4. Administrator password generation (user details screen)

- [x] 4.1 Wire the reset link to open the user details screen for the requesting user and verify opening a valid link navigates to that user's details screen
- [x] 4.2 Implement the administrator password-generation action on the user details screen and verify the generated password is assigned to the user
- [x] 4.3 Add unit tests for password generation and verify the tests pass

## 5. Email and notification to the user on password update

- [x] 5.1 Implement automatic email to the user when the administrator saves the new password via internal-mail and verify the user receives a new-password email
- [x] 5.2 Implement automatic in-app notification to the user via notification-center when the password is saved and verify the notification is created for the user
- [x] 5.3 Add unit tests for the notification behavior and verify the tests pass

## 6. Administrator-initiated reset from the profile screen

- [x] 6.1 Add Forgot password handling on the profile screen for signed-in administrators that auto-generates a password and sends a notification to the user (no administrator email step) and verify an administrator trigger generates a password and notifies the user
- [x] 6.2 Add unit tests for the admin-initiated flow and verify the tests pass

## 7. Integration and generic-error behavior

- [x] 7.1 Add rate limiting on reset requests per user and per administrator recipient and verify rapid requests are throttled
- [x] 7.2 Ensure failed reset attempts return a generic, non-enumerating outcome and verify the response does not reveal whether a user exists
- [x] 7.3 Add end-to-end integration tests covering full reset flows (user-initiated and admin-initiated) and verify they pass
- [x] 7.4 Document the forgot-password feature behavior and link expiry in the change documentation and verify the document is present

## Workflow follow-up

- Archive the change after the project's review requirements are satisfied.
- Verify the archived result.
