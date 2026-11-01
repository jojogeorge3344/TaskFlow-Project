import { TaskCard, TaskStatus } from '../../task-management/models/task.model';

export interface BoardColumn {
  status: TaskStatus;
  tasks: TaskCard[];
}
