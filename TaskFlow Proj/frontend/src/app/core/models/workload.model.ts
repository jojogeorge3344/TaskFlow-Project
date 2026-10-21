/** One employee's task load on the Employee Workload screen (Screen 16). Counts use the same
 * open/overdue rules as the dashboards so the figures agree across screens. */
export interface EmployeeWorkload {
  userId: number;
  fullName: string;
  projectId: number | null;
  projectName: string | null;
  activeTasks: number;
  highPriorityTasks: number;
  overdueTasks: number;
  completedTasks: number;
}

/** Totals derived from the rendered workload rows, used for the summary strip and the high-load rule. */
export interface WorkloadSummary {
  headcount: number;
  totalActive: number;
  averageActive: number;
  highLoadThreshold: number;
}
