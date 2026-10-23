export type AnalyticsBasis = 'Created' | 'Deadline' | 'Completed';

export const ANALYTICS_BASES: readonly AnalyticsBasis[] = ['Created', 'Deadline', 'Completed'];

export type DrillBucket = 'All' | 'Pending' | 'InProgress' | 'Completed' | 'Overdue';

export type DrillDeadlineBucket = 'Overdue' | 'DueToday' | 'Next7Days' | 'Later';

export type TaskBucket = 'Pending' | 'InProgress' | 'Completed' | 'Overdue';

export interface BucketCounts {
  pending: number;
  inProgress: number;
  completed: number;
  overdue: number;
  total: number;
  open: number;
  completionPercent: number;
}

export interface AppliedAnalyticsFilter {
  from: string;
  to: string;
  basis: AnalyticsBasis;
  departmentId: number | null;
  projectId: number | null;
  employeeId: number | null;
  managerId: number | null;
}

export interface AnalyticsSummary {
  totalEmployees: number;
  totalManagers: number;
  totalDepartments: number;
  totalProjects: number;
  tasks: BucketCounts;
  totalTasks: number;
  completionPercent: number;
}

export interface PriorityCount {
  priority: string;
  count: number;
}

export interface DeadlineBuckets {
  overdue: number;
  dueToday: number;
  next7Days: number;
  later: number;
}

export interface TrendPoint {
  date: string;
  created: number;
  completed: number;
}

export interface AnalyticsTrend {
  granularity: 'day' | 'week';
  points: TrendPoint[];
}

export interface DepartmentAnalytics {
  departmentId: number | null;
  name: string;
  tasks: BucketCounts;
}

export interface EmployeeAnalytics {
  userId: number;
  name: string;
  tasks: BucketCounts;
}

export interface EmployeeAnalyticsSection {
  items: EmployeeAnalytics[];
  totalEmployees: number;
}

export interface ManagerAnalytics {
  managerId: number;
  name: string;
  departments: number;
  employees: number;
  projects: number;
  tasks: BucketCounts;
}

export interface ProjectAnalytics {
  projectId: number | null;
  name: string;
  tasks: BucketCounts;
  projectedEnd: string | null;
  outstanding: number;
}

export interface AdminAnalytics {
  filter: AppliedAnalyticsFilter;
  summary: AnalyticsSummary;
  tasksByStatus: BucketCounts;
  tasksByPriority: PriorityCount[];
  deadlineBuckets: DeadlineBuckets;
  trend: AnalyticsTrend;
  byDepartment: DepartmentAnalytics[];
  byEmployee: EmployeeAnalyticsSection;
  byManager: ManagerAnalytics[];
  byProject: ProjectAnalytics[];
}

export interface AnalyticsOption {
  id: number;
  name: string;
}

export interface AnalyticsFilterOptions {
  today: string;
  timezone: string;
  departments: AnalyticsOption[];
  projects: AnalyticsOption[];
  employees: AnalyticsOption[];
  managers: AnalyticsOption[];
}

export interface AnalyticsFilterState {
  from: string;
  to: string;
  basis: AnalyticsBasis;
  departmentId: number | null;
  projectId: number | null;
  employeeId: number | null;
  managerId: number | null;
}

export interface TeamSummary {
  teamSize: number;
  totalProjects: number;
  tasks: BucketCounts;
  totalTasks: number;
  completionPercent: number;
}

/** One team member. The flags are computed by the server so the thresholds live in one place. */
export interface TeamEmployee {
  userId: number;
  name: string;
  tasks: BucketCounts;
  highWorkload: boolean;
  hasOverdue: boolean;
}

export interface DelayedTask {
  taskId: number;
  taskCode: string;
  title: string;
  assigneeName: string;
  projectName: string | null;
  daysOverdue: number;
}

export interface ProjectAttention {
  projectId: number;
  name: string;
  overdueTasks: number;
}

export interface TeamAttention {
  delayedTasks: DelayedTask[];
  projectsNeedingAttention: ProjectAttention[];
}

/** The Manager analytics response. It has no department section: managed departments only decide who is on the team. */
export interface TeamAnalytics {
  filter: AppliedAnalyticsFilter;
  summary: TeamSummary;
  tasksByStatus: BucketCounts;
  tasksByPriority: PriorityCount[];
  byEmployee: TeamEmployee[];
  byProject: ProjectAnalytics[];
  attention: TeamAttention;
}

export interface TeamFilterOptions {
  today: string;
  timezone: string;
  employees: AnalyticsOption[];
  projects: AnalyticsOption[];
}

export type EmployeePeriod = '7d' | '30d' | 'month';

export const EMPLOYEE_PERIODS: readonly { key: EmployeePeriod; label: string }[] = [
  { key: '7d', label: 'Last 7 days' },
  { key: '30d', label: 'Last 30 days' },
  { key: 'month', label: 'This month' }
];

/** Aggregate department figures. There is no per-person field. `tasks` is null when the department is too small to
 * show a breakdown (`limited`), in which case only `totalTasks` is given. */
export interface EmployeeDepartment {
  name: string;
  limited: boolean;
  totalTasks: number;
  tasks: BucketCounts | null;
}

export interface EmployeeSummary {
  totalTasks: number;
  completionPercent: number;
  completed: number;
  outstanding: number;
}

export interface EmployeeDeadline {
  taskId: number;
  taskCode: string;
  title: string;
  priority: string;
  deadline: string;
  daysRemaining: number;
  isOverdue: boolean;
  daysOverdue: number;
}

export interface EmployeeProject {
  projectId: number;
  name: string;
  totalTasks: number;
  completedTasks: number;
  progressPercent: number;
  latestDeadline: string | null;
}

/** The signed-in employee's own analytics. The server derives the subject from the session. */
export interface EmployeeOwnAnalytics {
  period: 'Last7Days' | 'Last30Days' | 'ThisMonth';
  from: string;
  to: string;
  department: EmployeeDepartment | null;
  summary: EmployeeSummary;
  tasksByStatus: BucketCounts;
  tasksByPriority: PriorityCount[];
  upcomingDeadlines: EmployeeDeadline[];
  projects: EmployeeProject[];
  trend: TrendPoint[];
  completedInPeriod: number;
}
