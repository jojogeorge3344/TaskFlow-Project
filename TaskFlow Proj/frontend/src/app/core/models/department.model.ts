export interface Department {
  id: number;
  name: string;
  description?: string | null;
  createdAt: string;
}

export interface DepartmentWithCount extends Department {
  managerCount: number;
  employeeCount: number;
}

export interface CreateDepartmentRequest {
  name: string;
  description?: string | null;
}

export interface UpdateDepartmentRequest {
  name: string;
  description?: string | null;
}

/** One manager or employee of a department, as the members endpoint reports them. */
export interface MemberRef {
  id: number;
  fullName: string;
  username: string;
  email: string;
  isActive: boolean;
}

export interface DepartmentMembers {
  departmentId: number;
  managers: MemberRef[];
  employees: MemberRef[];
}

/** Replaces the department's managers and employees in one call; employees are mapped onto User.DepartmentId. */
export interface UpdateDepartmentMembersRequest {
  managerIds: number[];
  employeeIds: number[];
}
