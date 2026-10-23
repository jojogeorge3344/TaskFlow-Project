# Forgot password — feature behaviour

TaskFlow has no public "reset it yourself" page. A password can only be replaced by an Administrator, either
because an employee asked for help or because an Administrator asked for one from the login page.

## Two entry points, one result

Both flows end the same way: the system assigns a newly generated password, ends every existing session of that
account, emails the new password through internal mail, and creates an in-app notification. The plaintext password
is never returned by the API.

### 1. Employee-initiated (login or profile screen)

1. The user clicks **Forgot password?** on the login screen (after typing their email or username) or on **My
   profile**.
2. `POST /api/v1/auth/forgot-password` with `{ "identifier": "<email or username>" }` creates a one-time token and
   places a system mail in the first active Administrator's mailbox.
3. The mail contains a link of the form
   `{App:BaseUrl}/reset-password?token=<token>&userId=<id>`.
4. The Administrator opens the link. The frontend (`/reset-password`) validates it through
   `GET /api/v1/auth/reset-password/{token}` and then opens that employee's details in **Employee Management**.
5. The details panel offers **Generate new password**. It calls `POST /api/v1/auth/reset-password` with
   `{ "token": "<token>" }`. The user's old password stops working immediately, their sessions are signed out, and
   the new password arrives in their mailbox together with an in-app notification.

### 2. Administrator-initiated (login page)

An Administrator who clicks **Forgot password?** with their own identifier does not need a second step: the password
is generated immediately and mailed to them. No reset link is minted and no administrator mail is sent.

## Link lifetime and single use

- A reset link is valid for **5 minutes** from creation (`ExpiresAt = CreatedAt + 5 minutes`).
- A link can be used **once**. Consuming it marks the token used and records `ConsumedAt`.
- Expiry is checked before single-use, so an aged link reports "Link not valid or expired." rather than a
  misleading "already used" state.
- Tokens are stored only as a SHA-256 hash; the plaintext token exists only in the emailed link.
- The link's API calls (`GET` and `POST /api/v1/auth/reset-password`) require the `employees.manage` permission, so a
  signed-in non-Administrator cannot generate a password with a leaked link.

## What an outsider can learn

`POST /api/v1/auth/forgot-password` always answers:

```json
{ "message": "If an account matches, a password reset has been initiated." }
```

The status and body are identical for a known account, an unknown identifier, an inactive account, and a throttled
request, so the endpoint cannot be used to enumerate users. Requests are throttled per requesting account (3 per 5
minutes) and per Administrator recipient (10 per 5 minutes); a throttled request is silently dropped but still
receives the generic response.

## Preconditions

- The Administrator must be signed in; opening a reset link while signed out leads to the login screen.
- The user must have an email address on file, because the new password is delivered by mail.
