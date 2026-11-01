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

/** One entry of the module-based permission catalogue: Module > Screen > Section > Feature (name) > Action. */
export interface PermissionInfo {
  /** 0 for always-on features, which are not stored as grants. */
  id: number;
  key: string;
  name: string;
  description: string;
  /** The module, kept for older callers. */
  group: string;
  /** False for permissions only the Administrator role may hold (never shown as a tickable box). */
  isAssignable: boolean;
  module: string;
  screen: string;
  /** The tab or section on the screen; empty when the permission belongs to the screen itself. */
  section: string;
  action: string;
  /** A permission that must also be held, normally the screen's View. */
  requires: string | null;
  /** Permissions sharing a scope group are levels of one selector; a higher rank includes the lower ones. */
  scopeGroup: string | null;
  scopeRank: number;
  /** Every user has it and it cannot be switched off. */
  alwaysGranted: boolean;
}

export interface SaveRoleRequest {
  name: string;
  permissionKeys: string[];
}

/** Ids of the system roles, fixed by the API. */
export const SYSTEM_ROLE_IDS = { Administrator: 1, Manager: 2, Employee: 3 } as const;
