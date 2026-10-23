import { AnalyticsBasis, DrillBucket, DrillDeadlineBucket } from './analytics.model';

export type TaskStatus = 'ToDo' | 'InProgress' | 'InQA' | 'Blocked' | 'Completed' | 'Cancelled';
export type TaskPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type TaskStage = 'Upcoming' | 'DueToday' | 'Overdue' | 'Completed';

export const BOARD_STATUSES: TaskStatus[] = ['ToDo', 'InProgress', 'InQA', 'Blocked', 'Completed'];

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

export type ReminderFrequency = 'Daily' | 'Weekly';

export const TASK_STATUSES: TaskStatus[] = ['ToDo', 'InProgress', 'InQA', 'Blocked', 'Completed', 'Cancelled'];
export const TASK_PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High', 'Critical'];

/** A row on the paginated Task List (Screen 7); stage and overdue are derived server-side. */
export interface TaskListItem {
  id: number;
  taskCode: string;
  title: string;
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
  isOverdue: boolean;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface TaskListFilter {
  assignedToUserId?: number | null;
  status?: TaskStatus | null;
  priority?: TaskPriority | null;
  deadlineFrom?: string | null;
  deadlineTo?: string | null;
  search?: string | null;
  page?: number;
  pageSize?: number;
  bucket?: DrillBucket | null;
  deadlineBucket?: DrillDeadlineBucket | null;
  departmentId?: number | null;
  projectId?: number | null;
  employeeId?: number | null;
  managerId?: number | null;
  from?: string | null;
  to?: string | null;
  basis?: AnalyticsBasis | null;
}

export interface CreateTaskRequest {
  title: string;
  description: string | null;
  assignedToUserId: number;
  priority: TaskPriority;
  startDate: string;
  deadline: string;
  estimatedHours: number;
  additionalRecipientUserIds: number[];
  /** null lets the server apply the configured reminder defaults. */
  firstReminderDays: number | null;
  secondReminderDays: number | null;
  overdueReminderFrequency: ReminderFrequency | null;
}
