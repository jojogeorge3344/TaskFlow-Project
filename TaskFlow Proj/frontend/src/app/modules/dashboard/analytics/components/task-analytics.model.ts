import { DrillDeadlineBucket } from '../models/analytics.model';

export interface DeadlineRow {
  key: DrillDeadlineBucket;
  label: string;
  value: number;
  width: number;
}
