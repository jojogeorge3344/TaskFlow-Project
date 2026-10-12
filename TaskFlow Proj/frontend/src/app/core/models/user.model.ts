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
}

export interface UserStatusRequest {
  isActive: boolean;
}
