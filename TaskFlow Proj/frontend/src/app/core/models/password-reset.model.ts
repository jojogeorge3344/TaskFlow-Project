export type PasswordResetOrigin = 'LoginScreen' | 'ProfileScreen';
export type PasswordResetStatus = 'Pending' | 'Completed' | 'Failed';

/** One row of the administrator's Password reset requests list. Carries no password material. */
export interface PasswordResetRequest {
  referenceId: string;
  userId: number;
  fullName: string;
  username: string;
  email: string;
  roleName: string;
  origin: PasswordResetOrigin;
  status: PasswordResetStatus;
  createdAt: string;
  completedAt: string | null;
  adminNotified: boolean;
  userNotified: boolean;
  temporaryPasswordIssued: boolean;
  failureReason: string | null;
}

/** The generic confirmation every Forgot Password submission gets, known account or not. */
export interface PasswordResetConfirmation {
  message: string;
}

/** The Login Screen notice read: one field, identical in shape whether or not a notice exists. */
export interface PasswordResetNotice {
  noticeAvailable: boolean;
}

/** What the Login Screen learns from an encrypted login-link token: the email to pre-fill, or nothing at all
 * for a bad, expired or superseded token (indistinguishable from an unknown account). */
export interface PasswordResetLinkResolution {
  email: string | null;
  noticeAvailable: boolean;
}

/** The password update behind the bell: the encrypted login-link token is the only proof and names the account. */
export interface CompletePasswordReset {
  token: string;
  newPassword: string;
}

/** The administrator's copyable login link for a completed request. It holds no password. */
export interface PasswordResetLoginLink {
  path: string;
  url: string | null;
}
