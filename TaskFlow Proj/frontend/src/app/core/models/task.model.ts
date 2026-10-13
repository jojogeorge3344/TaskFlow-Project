export type TaskStatus = 'ToDo' | 'InProgress' | 'Blocked' | 'Completed' | 'Cancelled';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type TaskStage = 'Upcoming' | 'DueToday' | 'Overdue' | 'Completed';

export const BOARD_STATUSES: TaskStatus[] = ['ToDo', 'InProgress', 'Blocked', 'Completed'];

export interface TaskCard {
  id: number;
  taskCode: string;
  title: string;
  assignedToUserId: number;
  assignedToName: string;
  priority: TaskPriority;
  deadline: string;
  status: TaskStatus;
  isOverdue: boolean;
}

export interface TaskRecipient {
  userId: number;
  fullName: string;
}

export interface TaskDetails {
  id: number;
  taskCode: string;
  title: string;
  description: string | null;
  assignedToUserId: number;
  assignedToName: string;
  createdByUserId: number;
  createdByName: string;
  priority: TaskPriority;
  startDate: string;
  deadline: string;
  estimatedHours: number;
  status: TaskStatus;
  stage: TaskStage;
  recipients: TaskRecipient[];
  createdAt: string;
  updatedAt: string;
}

export interface TaskComment {
  id: number;
  body: string;
  authorUserId: number;
  authorName: string;
  createdAt: string;
}

export interface ActivityEntry {
  id: number;
  eventType: string;
  actorUserId: number;
  actorName: string;
  oldValue: string | null;
  newValue: string | null;
  message: string;
  createdAt: string;
}

export interface UpdateTaskRequest {
  title: string;
  description: string | null;
  priority: TaskPriority;
  startDate: string;
  deadline: string;
  estimatedHours: number;
}

export interface UpdateTaskStatusRequest {
  status: TaskStatus;
}

export interface ReassignTaskRequest {
  newAssigneeUserId: number;
  reason: string;
}

export interface AddCommentRequest {
  body: string;
}

export interface SetRecipientsRequest {
  recipientUserIds: number[];
}