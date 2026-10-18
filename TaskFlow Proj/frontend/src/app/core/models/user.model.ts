export type Role = 'Administrator' | 'Manager' | 'Employee';

export interface User {
  id: number;
  employeeCode?: string | null;
  fullName: string;
  username: string;
  email: string;
  role: Role;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  lastLoginAt?: string | null;
  departmentId?: number | null;
  departmentName?: string | null;
}

export interface CreateUserRequest {
  employeeCode?: string | null;
  fullName: string;
  username: string;
  email: string;
  password: string;
  role: Role;
  isActive: boolean;
  departmentId?: number | null;
}

export interface UpdateUserRequest {
  employeeCode?: string | null;
  fullName: string;
  username: string;
  email: string;
  role: Role;
  departmentId?: number | null;
  /** New password; omit or leave empty to keep the current one. Changing it (or the username) ends the user's sessions. */
  password?: string | null;
}

export interface UserStatusRequest {
  isActive: boolean;
}

/** A self-service profile save (Screen 19). Only full name and email are changeable. */
export interface UpdateOwnProfile {
  fullName: string;
  email: string;
}

/** A self-service password change (Screen 19). Requires the current password. */
export interface ChangePassword {
  currentPassword: string;
  newPassword: string;
}
