/** One employee's task load on the Employee Workload screen (Screen 16). Counts use the same
 * open/overdue rules as the dashboards so the figures agree across screens. */
export interface EmployeeWorkload {
  userId: number;
  fullName: string;
  /** Every project the employee is on, with their percentage. */
  projects: { projectId: number; projectName: string; positionId: number; positionName: string; percent: number }[];
  allocatedPercent: number;
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
