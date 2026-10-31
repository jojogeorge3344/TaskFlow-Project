import { TaskPriority, TaskStatus } from '../../core/models';

/** The filters as the user typed them: empty string means "any". */
export interface Filters {
  status: TaskStatus | '';
  priority: TaskPriority | '';
  userId: number | null;
  positionId: number | null;
  from: string;
  to: string;
}
