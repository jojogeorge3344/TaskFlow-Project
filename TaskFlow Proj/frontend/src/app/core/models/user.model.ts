/** A role name: Administrator, Manager, Employee, or a custom role an administrator created. */
export type Role = string;

export interface User {
  id: number;
  employeeCode?: string | null;
  fullName: string;
  username: string;
  email: string;
  /** The role's name, for display. */
  role: Role;
  roleId: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  lastLoginAt?: string | null;
  departmentId?: number | null;
  departmentName?: string | null;
  /** Who this user reports to. Recorded data only; it does not change what anyone can see. */
  managerId?: number | null;
  managerName?: string | null;
  /** For a manager: the departments they are assigned to. */
  managerDepartments?: ManagerDepartmentRef[];
  /** For a manager: the people who report to them. */
  reports?: ManagerReportRef[];
}

export interface ManagerDepartmentRef {
  id: number;
  name: string;
}

export interface ManagerReportRef {
  id: number;
  fullName: string;
}

/** Replaces a user's manager and, for a manager, their departments. Administrator only. */
export interface ManagerAssignmentRequest {
  managerId: number | null;
  managerDepartmentIds: number[];
}

export interface CreateUserRequest {
  employeeCode?: string | null;
  fullName: string;
  username: string;
  email: string;
  password: string;
  roleId: number;
  isActive: boolean;
  departmentId?: number | null;
}

export interface UpdateUserRequest {
  employeeCode?: string | null;
  fullName: string;
  username: string;
  email: string;
  roleId: number;
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
