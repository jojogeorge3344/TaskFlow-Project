/** A role name: Administrator, Manager, Employee, or a custom role an administrator created. */
export type Role = string;

export interface User {
  id: number;
  employeeCode?: string | null;
  fullName: string;
  username: string;
  email: string;
  /** Contact number in international format. Not unique: email is the only unique identity. */
  mobileNumber?: string | null;
  /** Whether authenticator-app MFA is on, and how many unused backup codes remain. The key is never sent. */
  mfaEnabled?: boolean;
  backupCodesRemaining?: number;
  /** The role's name, for display. */
  role: Role;
  roleId: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
  lastLoginAt?: string | null;
  /** The projects the user is assigned to, each with a percentage and the position held there. */
  projectAllocations: ProjectAllocation[];
  /** Sum of the percentages across all projects; never above 100. */
  totalAllocatedPercent: number;
  remainingPercent: number;
  /** The departments the user belongs to. */
  departments: UserDepartmentRef[];
  /** Derived: the managers of the user's departments. Never set on the user directly. */
  managers: UserManagerRef[];
  /** For a manager: the projects they are assigned to. */
  managerProjects?: ManagerProjectDto[];
  /** For a manager: the people who report to them. */
  reports?: ManagerReportRef[];
}

export interface ProjectAllocation {
  projectId: number;
  projectName: string;
  positionId: number;
  positionName: string;
  percent: number;
}

export interface UserDepartmentRef {
  id: number;
  name: string;
}

export interface UserManagerRef {
  id: number;
  fullName: string;
}

/** One project row sent from a form: the project, the position held there and a whole percentage (1 to 100). */
export interface ProjectAllocationInput {
  projectId: number;
  positionId: number;
  percent: number;
}

/** The most one person can be allocated across all projects. */
export const MAX_TOTAL_PERCENT = 100;

export interface ManagerProjectDto {
  id: number;
  name: string;
}

export interface ManagerReportRef {
  id: number;
  fullName: string;
}

export interface CreateUserRequest {
  employeeCode?: string | null;
  fullName: string;
  username: string;
  email: string;
  password: string;
  roleId: number;
  isActive: boolean;
  /** Omit to leave unchanged; an empty list clears. */
  projectAllocations?: ProjectAllocationInput[] | null;
  departmentIds?: number[] | null;
}

export interface UpdateUserRequest {
  employeeCode?: string | null;
  fullName: string;
  username: string;
  email: string;
  roleId: number;
  projectAllocations?: ProjectAllocationInput[] | null;
  departmentIds?: number[] | null;
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
  /** Omit to keep the current number; an empty string clears it. */
  mobileNumber?: string;
}

/** A self-service password change (Screen 19). The authenticator code is the proof of identity; no current password. */
export interface ChangePassword {
  newPassword: string;
  /** The current code from the authenticator app (or a backup code). Required while two-step verification is on. */
  mfaCode?: string;
}
