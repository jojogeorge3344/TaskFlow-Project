import { TaskPriority, TaskStatus } from './task.model';

/** Filters for a task report (Screen 17). Every field is optional; an omitted filter leaves that
 * dimension unrestricted. The date range filters the task deadline inclusively. */
export interface ReportFilter {
  deadlineFrom?: string | null;
  deadlineTo?: string | null;
  assignedToUserId?: number | null;
  departmentId?: number | null;
  status?: TaskStatus | null;
  priority?: TaskPriority | null;
}

/** Totals for the filtered scope, rendered as the headline figures and the completion summary. */
export interface ReportSummary {
  totalTasks: number;
  pendingTasks: number;
  completedTasks: number;
  overdueTasks: number;
}
