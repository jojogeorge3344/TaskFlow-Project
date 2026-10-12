export interface Department {
  id: number;
  name: string;
  description?: string | null;
  createdAt: string;
}

export interface DepartmentWithCount {
  id: number;
  name: string;
  description?: string | null;
  createdAt: string;
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
