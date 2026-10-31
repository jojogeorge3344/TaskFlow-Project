/** One project row of the allocation editor. A row is complete once it has a project, a position and a whole percentage. */
export interface AllocationRow {
  key: number;
  projectId: number | null;
  positionId: number | null;
  percent: number | null;
}
