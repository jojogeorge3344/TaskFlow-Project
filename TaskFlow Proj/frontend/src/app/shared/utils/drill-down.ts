import { ParamMap } from '@angular/router';
import { ANALYTICS_BASES, AnalyticsBasis, AnalyticsFilterState, DrillBucket, DrillDeadlineBucket } from '../../modules/dashboard/analytics/models/analytics.model';
import { TASK_PRIORITIES, TaskListFilter, TaskPriority } from '../../modules/monitoring/task-management/models/task.model';
import { toQueryParams } from './analytics-filter';

export const DRILL_BUCKETS: readonly DrillBucket[] = ['All', 'Pending', 'InProgress', 'Completed', 'Overdue'];
export const DRILL_DEADLINE_BUCKETS: readonly DrillDeadlineBucket[] = ['Overdue', 'DueToday', 'Next7Days', 'Later'];

/** What a chart click selects. A null id means the chart's "No department" / "No project" group. */
export interface DrillTarget {
  bucket?: DrillBucket;
  priority?: TaskPriority;
  deadlineBucket?: DrillDeadlineBucket;
  departmentId?: number | null;
  projectId?: number | null;
  employeeId?: number;
  managerId?: number;
  range?: { from: string; to: string; basis: Extract<AnalyticsBasis, 'Created' | 'Completed'> };
}

export const NO_GROUP_ID = 0;

/** The one place that turns a chart click plus the dashboard filters into task-list query params. */
export function drillDownQuery(filter: AnalyticsFilterState, target: DrillTarget): Record<string, string> {
  const params = toQueryParams(filter);

  if (target.departmentId !== undefined) params['departmentId'] = String(target.departmentId ?? NO_GROUP_ID);
  if (target.projectId !== undefined) params['projectId'] = String(target.projectId ?? NO_GROUP_ID);
  if (target.employeeId !== undefined) params['employeeId'] = String(target.employeeId);
  if (target.managerId !== undefined) params['managerId'] = String(target.managerId);

  if (target.range) {
    params['from'] = target.range.from;
    params['to'] = target.range.to;
    params['basis'] = target.range.basis;
  }

  if (target.deadlineBucket) {
    params['deadlineBucket'] = target.deadlineBucket;
  } else {
    params['bucket'] = target.bucket ?? 'All';
  }

  if (target.priority) params['priority'] = target.priority;

  return params;
}

/** Task-list query for an Employee chart click. The employee is always the caller, so the list holds exactly the
 * assigned-or-shared tasks the dashboard counted; the server scopes the list to the caller again whatever is in the URL. */
export function employeeDrillQuery(userId: number, target: DrillTarget): Record<string, string> {
  const params: Record<string, string> = { employeeId: String(userId) };

  if (target.projectId !== undefined) params['projectId'] = String(target.projectId ?? NO_GROUP_ID);
  if (target.range) {
    params['from'] = target.range.from;
    params['to'] = target.range.to;
    params['basis'] = target.range.basis;
  }

  if (target.deadlineBucket) {
    params['deadlineBucket'] = target.deadlineBucket;
  } else {
    params['bucket'] = target.bucket ?? 'All';
  }

  if (target.priority) params['priority'] = target.priority;

  return params;
}

export interface DrillDownFilter {
  request: TaskListFilter;
  summary: string;
}

function integer(value: string | null): number | undefined {
  if (value === null || !/^\d+$/.test(value)) {
    return undefined;
  }
  return Number(value);
}

function day(value: string | null): string | undefined {
  return value !== null && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : undefined;
}

/** Reads the drill-down query params back into a task-list request, ignoring anything malformed. */
export function parseDrillDown(params: ParamMap): DrillDownFilter | null {
  const request: TaskListFilter = {};
  const parts: string[] = [];

  const bucket = params.get('bucket');
  if (bucket && (DRILL_BUCKETS as readonly string[]).includes(bucket)) {
    request.bucket = bucket as DrillBucket;
    parts.push(bucket === 'All' ? 'All counted tasks' : bucket.replace('InProgress', 'In progress'));
  }

  const deadlineBucket = params.get('deadlineBucket');
  if (deadlineBucket && (DRILL_DEADLINE_BUCKETS as readonly string[]).includes(deadlineBucket)) {
    request.deadlineBucket = deadlineBucket as DrillDeadlineBucket;
    parts.push(`Deadline: ${deadlineBucket.replace('DueToday', 'due today').replace('Next7Days', 'next 7 days').toLowerCase()}`);
  }

  const priority = params.get('priority');
  if (priority && (TASK_PRIORITIES as readonly string[]).includes(priority)) {
    request.priority = priority as TaskPriority;
    parts.push(`${priority} priority`);
  }

  const departmentId = integer(params.get('departmentId'));
  if (departmentId !== undefined) {
    request.departmentId = departmentId;
    parts.push(departmentId === NO_GROUP_ID ? 'No department' : 'One department');
  }

  const projectId = integer(params.get('projectId'));
  if (projectId !== undefined) {
    request.projectId = projectId;
    parts.push(projectId === NO_GROUP_ID ? 'No project' : 'One project');
  }

  const employeeId = integer(params.get('employeeId'));
  if (employeeId !== undefined) {
    request.employeeId = employeeId;
    parts.push('One employee');
  }

  const managerId = integer(params.get('managerId'));
  if (managerId !== undefined) {
    request.managerId = managerId;
    parts.push('One manager’s team');
  }

  const from = day(params.get('from'));
  const to = day(params.get('to'));
  if (from && to) {
    const basis = params.get('basis');
    request.from = from;
    request.to = to;
    request.basis = basis && (ANALYTICS_BASES as readonly string[]).includes(basis) ? (basis as AnalyticsBasis) : 'Created';
    parts.push(`${from} to ${to} (${request.basis.toLowerCase()})`);
  }

  return Object.keys(request).length === 0 ? null : { request, summary: parts.join(' · ') };
}
