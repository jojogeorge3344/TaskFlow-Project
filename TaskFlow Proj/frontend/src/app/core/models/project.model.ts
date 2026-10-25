import { TaskPriority, TaskStatus } from './task.model';

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
  /** Full names of the people flagged as this project's managers. */
  managers: string[];
}

export interface CreateProjectRequest {
  name: string;
  description?: string | null;
}

export interface UpdateProjectRequest {
  name: string;
  description?: string | null;
}

export interface Position {
  id: number;
  name: string;
}

/** A person on a project's team with the position they hold there and their percentage. */
export interface ProjectMember {
  userId: number;
  fullName: string;
  email: string;
  positionId: number;
  positionName: string;
  percent: number;
  /** Flagged as one of the project's managers. */
  isManager: boolean;
  /** Holds the Manager role, so the flag may be set. */
  isManagerRole: boolean;
}

export interface ProjectMemberInput {
  userId: number;
  positionId: number;
  percent: number;
  isManager: boolean;
}

/** One task of a project, with the assignee's role on that project (null when they are not on its team). */
export interface ProjectTask {
  id: number;
  taskCode: string;
  title: string;
  status: TaskStatus;
  priority: TaskPriority;
  startDate: string;
  deadline: string;
  assignedToUserId: number;
  assignedToName: string;
  assigneePositionId: number | null;
  assigneePositionName: string | null;
}

export interface ProjectTaskFilter {
  status?: TaskStatus | null;
  priority?: TaskPriority | null;
  deadlineFrom?: string | null;
  deadlineTo?: string | null;
  assignedToUserId?: number | null;
  positionId?: number | null;
}
