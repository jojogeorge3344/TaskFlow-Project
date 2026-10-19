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
