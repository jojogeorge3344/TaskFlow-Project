/** One person on the team being edited. `capacity` is the most percent this person can take on this project, when it is
 * known (they were picked here with a known remaining share); the server stays the final judge. */
export interface TeamRow {
  key: number;
  userId: number | null;
  fullName: string;
  positionId: number | null;
  percent: number | null;
  capacity: number | null;
  /** Flagged as a manager of the project. */
  isManager: boolean;
  /** Holds the Manager role, so the flag may be set. */
  canManage: boolean;
}
