export interface ManagerDashboardStats {
  totalTasks: number;
  pendingTasks: number;
  tasksDueToday: number;
  upcomingDeadlines: number;
  overdueTasks: number;
  completedTasks: number;
}

export interface EmployeeDashboardStats {
  assignedTasks: number;
  pendingTasks: number;
  tasksDueToday: number;
  overdueTasks: number;
  inProgressTasks: number;
  completedTasks: number;
}
