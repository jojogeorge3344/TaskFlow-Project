export type MemberSort = 'name-asc' | 'name-desc' | 'selected' | 'role';

export interface MemberOption {
  id: number;
  fullName: string;
  username: string;
  /** Whether the person is saved as a manager or as an employee of the department. */
  kind: 'manager' | 'employee';
  /** A current member who is no longer an active manager/employee; still listed so they are not silently dropped. */
  inactive: boolean;
}
