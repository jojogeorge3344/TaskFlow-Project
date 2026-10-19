/** A role as the Permissions Management screen and the employee form see it. */
export interface AccessRole {
  id: number;
  name: string;
  /** Administrator, Manager and Employee: read-only, never deleted. */
  isSystem: boolean;
  /** How many accounts hold the role; a role in use cannot be deleted. */
  userCount: number;
  permissionKeys: string[];
}

/** One entry of the fixed permission catalog. */
export interface PermissionInfo {
  id: number;
  key: string;
  name: string;
  description: string;
  group: string;
  /** False for permissions only the Administrator role may hold (never shown as a tickable box). */
  isAssignable: boolean;
}

export interface SaveRoleRequest {
  name: string;
  permissionKeys: string[];
}

/** Ids of the system roles, fixed by the API. */
export const SYSTEM_ROLE_IDS = { Administrator: 1, Manager: 2, Employee: 3 } as const;
