export type UserRole = 'Administrator' | 'Manager' | 'Employee';

export interface AuthUser {
  id: number;
  fullName: string;
  username: string;
  email: string;
  role: UserRole;
  employeeCode: string | null;
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
