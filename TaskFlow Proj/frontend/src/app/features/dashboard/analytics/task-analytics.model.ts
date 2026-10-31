import { DrillDeadlineBucket } from '../../../core/models';

export interface DeadlineRow {
  key: DrillDeadlineBucket;
  label: string;
  value: number;
  width: number;
}
