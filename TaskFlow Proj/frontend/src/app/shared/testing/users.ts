import { ProjectAllocation, User } from '../../modules/management/employee-management/models/user.model';

/** The fields every user now carries for projects, departments and managers, for fixtures that need none of them. */
export const NO_ALLOCATIONS = {
  projectAllocations: [] as ProjectAllocation[],
  totalAllocatedPercent: 0,
  remainingPercent: 100,
  departments: [] as { id: number; name: string }[],
  managers: [] as { id: number; fullName: string }[]
};

/** A user with one 100% project allocation. */
export function withProject(projectId: number, projectName: string, positionName = 'Member'): Pick<
  User,
  'projectAllocations' | 'totalAllocatedPercent' | 'remainingPercent'
> {
  return {
    projectAllocations: [{ projectId, projectName, positionId: 1, positionName, percent: 100 }],
    totalAllocatedPercent: 100,
    remainingPercent: 0
  };
}
