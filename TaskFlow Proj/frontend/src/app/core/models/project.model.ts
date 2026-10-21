export interface Project {
  id: number;
  name: string;
  description?: string | null;
  createdAt: string;
}

export interface ProjectWithCount {
  id: number;
  name: string;
  description?: string | null;
  createdAt: string;
  employeeCount: number;
}

export interface CreateProjectRequest {
  name: string;
  description?: string | null;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string | null;
}
