import { TaskCard, TaskStatus } from '../../core/models';

export interface BoardColumn {
  status: TaskStatus;
  tasks: TaskCard[];
}
