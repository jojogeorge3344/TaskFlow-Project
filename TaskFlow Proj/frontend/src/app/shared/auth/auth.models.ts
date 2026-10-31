/** A role name. Administrator, Manager and Employee are the system roles; an administrator can add more. */
export type UserRole = string;

export type SystemRoleName = 'Administrator' | 'Manager' | 'Employee';

export interface AuthUser {
  id: number;
  fullName: string;
  username: string;
  email: string;
  role: UserRole;
  employeeCode: string | null;
  /** Keys of the permissions the user's role holds, as of sign-in or the last refresh. The API decides every request. */
  permissions: string[];
  /** True while two-step verification is required but not set up yet; the app then shows only the setup screen. */
  mfaSetupRequired?: boolean;
}

export interface LoginRequest {
  emailOrUsername: string;
  password: string;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
}

/** What the API returns after a correct SMS code: a single-use token needed to set the new password. */
export interface RecoveryTokenResponse {
  resetToken: string;
}

/** Step 3 of the SMS password reset. */
export interface RecoverPasswordRequest {
  resetToken: string;
  newPassword: string;
  confirmPassword: string;
}

/** What `auth/login` returns instead of a session when the account has authenticator MFA on. */
export interface MfaChallenge {
  mfaRequired: true;
  challengeToken: string;
}

export function isMfaChallenge(value: AuthUser | MfaChallenge): value is MfaChallenge {
  return (value as MfaChallenge).mfaRequired === true;
}

/** Shown once when MFA is turned on or the backup codes are regenerated. */
export interface MfaBackupCodes {
  codes: string[];
}

/** Returned once when setup starts: the key to type in and the URI behind the QR code. */
export interface MfaSetup {
  key: string;
  provisioningUri: string;
}
