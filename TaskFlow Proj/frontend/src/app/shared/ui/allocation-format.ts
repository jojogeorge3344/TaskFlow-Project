import { ProjectAllocation } from '../../core/models';

/** "Apollo 60%", or "Apollo 60% (QA)" when the position is wanted too. */
export function allocationLabel(allocation: Pick<ProjectAllocation, 'projectName' | 'percent' | 'positionName'>, withPosition = false): string {
  const base = `${allocation.projectName} ${allocation.percent}%`;
  return withPosition && allocation.positionName ? `${base} (${allocation.positionName})` : base;
}

/** The projects of one person as one line: "Apollo 60% (QA), Zephyr 40% (Developer)"; "Unassigned" when there are none. */
export function allocationsText(allocations: readonly ProjectAllocation[] | null | undefined, withPosition = true): string {
  if (!allocations || allocations.length === 0) {
    return 'Unassigned';
  }
  return allocations.map((allocation) => allocationLabel(allocation, withPosition)).join(', ');
}
