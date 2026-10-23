## 1. Backend

- [x] 1.1 Remove `CurrentPassword` from `ChangePasswordRequest` and its `NotEmpty` rule in `ChangePasswordRequestValidator`
- [x] 1.2 In `UsersController.ChangeOwnPassword`, drop the attempt-limiter and `CheckOwnPasswordAsync` steps; keep `RequireCodeAsync`, and refuse when the user has no MFA enabled
- [x] 1.3 In `UserService.ChangeOwnPasswordAsync`, remove the current-password verification
- [x] 1.4 Update backend unit tests (`UpdateOwnProfileRequestValidatorTests`, `UserServiceSelfServiceTests`) for the new contract
- [ ] 1.5 Update `UserProfileEndpointsTests` (and any MFA step-up tests posting `currentPassword` to `me/password`): drop the wrong-current-password case, add wrong/missing code, success, weak password and sessions-ended cases

## 2. Frontend

- [x] 2.1 Remove `currentPassword` from `ChangePasswordRequest` (`user.model.ts`) and from `ProfileService.changePassword` callers
- [x] 2.2 On Profile, replace the inline change-password form with a Change password button, and remove the reveal-by-code logic (unlock form, relock timer, `checkMfaCode` use)
- [x] 2.3 Add the routed `profile/change-password` screen: new password and confirm with plain show/hide eyes, a Back button to Profile, no code box
- [x] 2.4 Add the code popup on submit: asks only for the authenticator code, sends one request with the new password and code, keeps the popup open with the server message on rejection, signs the user out on success
- [x] 2.5 Update and add specs for Profile, the new screen and the popup

## 3. Verification

- [ ] 3.1 Run backend unit and integration tests for users, profile and MFA, and the frontend profile and login specs; confirm they pass
- [ ] 3.2 Check in the browser: Profile has a Change password button, the screen has Back and no code box, the eye icons never ask for a code, the popup asks for the code on submit, a wrong code is rejected, and a valid one signs you out
