## Context

Today `POST /api/v1/users/me/password` (`UsersController.ChangeOwnPassword`) rate-limits and verifies `CurrentPassword` (`IAttemptLimiter.PasswordKey`, `CheckOwnPasswordAsync`), then calls `IMfaService.RequireCodeAsync`, then `UserService.ChangeOwnPasswordAsync`, which verifies the current password again before hashing the new one and ending sessions. The Profile form (`profile.component`) holds `currentPassword`, `newPassword`, `confirmPassword` and `mfaCode`, and sends them via `ProfileService.changePassword`. `MfaSetupGateMiddleware` blocks the API (except `/auth/*`, `/users/me/mfa/*`, `/users/me`) for users without MFA, so any caller reaching this endpoint has MFA on and `RequireCodeAsync` always enforces a code.

## Goals / Non-Goals

**Goals:** drop the current-password input and server check from this one flow; keep the code as the proof of identity; keep session ending and length rules.
**Non-Goals:** security setup, MFA setup/backup-code regeneration, password recovery and login are unchanged. No change to MFA lockout.

## Decisions

1. **Contract.** `ChangePasswordRequest` becomes `(NewPassword, MfaCode)`; `CurrentPassword` is removed. An unknown extra `currentPassword` JSON property is ignored by model binding, so old clients still work.
2. **Server enforcement of the code.** Keep `RequireCodeAsync`. Because it returns silently when MFA is off, add a defensive check in the controller: if the user has no MFA enabled, refuse (the gate already prevents this, so this is a guard against gate regressions rather than a new rule).
3. **Remove the limiter use here.** The password-guess budget existed only for the current-password check; wrong codes are already counted by the MFA lockout. `IAttemptLimiter.PasswordKey` stays if other flows use it; otherwise it can be left unused.
4. **Service.** `ChangeOwnPasswordAsync` drops its verification block; it hashes the new password, updates timestamps and ends sessions as before.
5. **Frontend.** A routed screen `profile/change-password` (`ChangePasswordComponent`) holds only `newPassword` and `confirmPassword` with plain show/hide eyes and a Back button to `/profile`. Submitting a valid form opens a modal that asks for the code; confirming sends one `changePassword({ newPassword, mfaCode })` request. A rejected code keeps the modal open with the server message; success clears the session and goes to login as before. Profile's inline form, `showNew/showConfirm` unlock logic, `checkMfaCode` use and relock timer are removed, and the Security area becomes a card with a Change password button. The backend `mfa/check` endpoint stays but Profile no longer calls it.

## Risks / Trade-offs

- A stolen access token plus a valid code can now change the password without knowing the old one. Mitigated by the code being short-lived and replay-protected, and by sessions ending on success.
- Overlap with unarchived changes: `settings-and-profile` and `authenticator-mfa` still describe the current-password requirement for this endpoint; this change supersedes it, so note it on archive.
- Existing tests that send `currentPassword` or assert "wrong current password" must be rewritten around the code.

## Open Questions

- Whether to remove the current-password requirement from the security setup screen and MFA setup as well (explicitly deferred).
