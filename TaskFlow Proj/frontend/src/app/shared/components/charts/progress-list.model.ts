export interface ProgressItem {
  key: number | string | null;
  label: string;
  percent: number;
  summary: string;
  note?: string;
}
