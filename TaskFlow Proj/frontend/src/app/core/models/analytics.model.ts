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
