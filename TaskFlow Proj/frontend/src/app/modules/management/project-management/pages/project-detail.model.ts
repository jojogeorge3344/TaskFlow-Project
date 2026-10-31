import { TaskPriority, TaskStatus } from '../../../monitoring/task-management/models/task.model';

/** The filters as the user typed them: empty string means "any". */
export interface Filters {
  status: TaskStatus | '';
  priority: TaskPriority | '';
  userId: number | null;
  positionId: number | null;
  from: string;
  to: string;
}
