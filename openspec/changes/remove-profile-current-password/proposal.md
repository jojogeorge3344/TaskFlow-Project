# Proposal

## Why

The Profile "Change password" form asks for the user's current password, but TaskFlow never stores it in a readable form, so the box can never be pre-filled and users must retype it. Every user already has an authenticator app (the MFA setup gate blocks the API until it is set up) and the same form already requires a fresh authenticator code, so the current password is a second, redundant proof on this screen.

## What Changes

- Change password becomes its own screen, opened from a **Change password** button on Profile, with a **Back** button to Profile. It has only **New password** and **Confirm new password**; the Current password field and the authenticator-code box are gone.
- The eye icons on those two fields only show or hide what was typed. They never ask for a code, and the "unlock viewing with a code" feature on Profile is removed.
- Clicking **Change password** opens a popup that asks only for the authenticator code. The new password is saved only if the server accepts the code; otherwise it is rejected and nothing changes.
- `POST /api/v1/users/me/password` no longer accepts or checks `currentPassword`. It requires a valid authenticator code (or unused backup code) and the new password, keeps the length rules, and ends every session of the user on success.
- Remove the per-user "wrong current password" attempt budget on this endpoint; wrong codes still count toward the existing MFA lockout.
- **BREAKING** (API): clients that send `currentPassword` to this endpoint have it ignored; the field is removed from the contract.
- Out of scope: security setup screen, `POST users/me/mfa/setup`, backup-code regeneration and password recovery keep their current behaviour.

## Capabilities

### New Capabilities
- `profile-password-change`: how a signed-in user changes their own password from Profile, including what proves their identity and what happens to sessions.

### Modified Capabilities
<!-- None as main specs: the earlier requirements live in the unarchived `settings-and-profile` and `authenticator-mfa` changes and are superseded for this endpoint by the new capability. -->

## Impact

- Frontend: `profile.component.html/.ts` (replace the inline form with a Change password button and remove the reveal-by-code logic), a new routed `profile/change-password` screen with a code popup, `core/models/user.model.ts` (`ChangePasswordRequest`), `profile.service.ts`, profile specs.
- Backend: `UsersController.ChangeOwnPassword` (drop password check and limiter), `ChangePasswordRequest` and its validator, `UserService.ChangeOwnPasswordAsync`, plus integration and unit tests that send `currentPassword`.
- Docs/OpenSpec: supersedes the "current password" parts of `settings-and-profile` (user-profile) and `authenticator-mfa` for this endpoint; archive order matters.
- Security trade-off: an attacker holding only a stolen session token now needs a valid authenticator code instead of code plus password.
- Assumption: scope is the Profile form and its endpoint only (option 2 from discussion).
