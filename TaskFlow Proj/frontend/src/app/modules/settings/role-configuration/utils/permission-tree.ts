import { Permissions } from '../../../../shared/auth/permissions';
import { PermissionInfo } from '../models/role.model';

/**
 * The permission catalogue as a tree: Module > Screen > Section > Feature. Pure functions only, so the rules for
 * ticking, scope levels and dependencies are testable without the screen. Role-specific copies never exist: every role
 * is shown against this same tree.
 */

export interface TreeFeature {
  id: string;
  name: string;
  description: string;
  /** One permission, or the levels of a scope selector ordered from the lowest. */
  permissions: PermissionInfo[];
  isScope: boolean;
}

export interface TreeSection {
  name: string;
  features: TreeFeature[];
}

export interface TreeScreen {
  name: string;
  sections: TreeSection[];
}

export interface TreeModule {
  name: string;
  screens: TreeScreen[];
}

export type CheckState = 'all' | 'some' | 'none';

/** Names for scope selectors, which have no single permission to take a name from. */
const SCOPE_LABELS: Record<string, { name: string; description: string }> = {
  'dashboard.view': {
    name: 'View dashboard',
    description: 'Which figures the dashboard shows: only the user\'s own, their team\'s, or the whole organization\'s.'
  },
  'tasks.view': {
    name: 'View tasks',
    description: 'Whether the user sees only tasks they created or are assigned to, or every task in the organization.'
  }
};

export function buildTree(catalog: PermissionInfo[]): TreeModule[] {
  const modules: TreeModule[] = [];

  for (const permission of catalog) {
    const module = findOrAdd(modules, permission.module, (name) => ({ name, screens: [] }));
    const screen = findOrAdd(module.screens, permission.screen, (name) => ({ name, sections: [] }));
    const section = findOrAdd(screen.sections, permission.section, (name) => ({ name, features: [] }));

    if (permission.scopeGroup) {
      const existing = section.features.find((feature) => feature.id === permission.scopeGroup);
      if (existing) {
        existing.permissions.push(permission);
        existing.permissions.sort((a, b) => a.scopeRank - b.scopeRank);
        continue;
      }

      const label = SCOPE_LABELS[permission.scopeGroup] ?? { name: permission.name, description: permission.description };
      section.features.push({
        id: permission.scopeGroup,
        name: label.name,
        description: label.description,
        permissions: [permission],
        isScope: true
      });
      continue;
    }

    section.features.push({
      id: permission.key,
      name: permission.name,
      description: permission.description,
      permissions: [permission],
      isScope: false
    });
  }

  return modules;
}

function findOrAdd<T extends { name: string }>(list: T[], name: string, create: (name: string) => T): T {
  let found = list.find((candidate) => candidate.name === name);
  if (!found) {
    found = create(name);
    list.push(found);
  }
  return found;
}

/** Every feature of a module, flattened. */
export function featuresOf(module: TreeModule): TreeFeature[] {
  return module.screens.flatMap((screen) => screen.sections.flatMap((section) => section.features));
}

export function featuresOfScreen(screen: TreeScreen): TreeFeature[] {
  return screen.sections.flatMap((section) => section.features);
}

/** The keys a role holds in effect: what is ticked, plus the lower levels of every scope selector. */
export function effectiveKeys(keys: readonly string[], catalog: PermissionInfo[]): Set<string> {
  const result = new Set(keys);
  for (const permission of catalog) {
    if (!permission.scopeGroup || !result.has(permission.key)) {
      continue;
    }

    for (const other of catalog) {
      if (other.scopeGroup === permission.scopeGroup && other.scopeRank < permission.scopeRank) {
        result.add(other.key);
      }
    }
  }
  return result;
}

/** Keys only the Administrator role holds: making an Administrator is never delegated. */
export const RESTRICTED_KEYS: readonly string[] = [Permissions.RolesAdministratorAssign];

/**
 * The catalogue as the signed-in user may use it: without the Administrator-only key, the privilege-raising entries
 * count as Administrator only, so they are not offered and cannot be ticked. The API refuses them as well.
 */
export function restrictForCaller(catalog: PermissionInfo[], canGiveRestricted: boolean): PermissionInfo[] {
  return canGiveRestricted
    ? catalog
    : catalog.map((permission) => (RESTRICTED_KEYS.includes(permission.key) ? { ...permission, isAssignable: false } : permission));
}

/** What an administrator can tick or clear: not always-on features and not Administrator-only ones. */
export function isSelectable(permission: PermissionInfo): boolean {
  return permission.isAssignable && !permission.alwaysGranted;
}

/** Whether a permission counts as enabled for the role (always-on features always do). */
export function isEnabled(permission: PermissionInfo, effective: ReadonlySet<string>): boolean {
  return permission.alwaysGranted || effective.has(permission.key);
}

/** The permissions a user can switch for this feature: all levels of a selector except the always-on lowest one. */
export function selectablePermissions(feature: TreeFeature): PermissionInfo[] {
  return feature.permissions.filter(isSelectable);
}

export function stateOf(permissions: PermissionInfo[], effective: ReadonlySet<string>): CheckState {
  const enabled = permissions.filter((permission) => isEnabled(permission, effective)).length;
  if (enabled === 0) {
    return 'none';
  }
  return enabled === permissions.length ? 'all' : 'some';
}

export function enabledCount(permissions: PermissionInfo[], effective: ReadonlySet<string>): number {
  return permissions.filter((permission) => isEnabled(permission, effective)).length;
}

/** Ticks or clears one permission. Ticking also ticks what it requires; clearing also clears what requires it. */
export function toggleKey(keys: readonly string[], key: string, checked: boolean, catalog: PermissionInfo[]): string[] {
  const result = new Set(keys);
  const byKey = new Map(catalog.map((permission) => [permission.key, permission]));

  if (checked) {
    let current: string | null | undefined = key;
    while (current) {
      const permission = byKey.get(current);
      if (!permission || !isSelectable(permission)) {
        break;
      }
      result.add(current);
      current = permission.requires;
    }
    return [...result];
  }

  const queue = [key];
  while (queue.length > 0) {
    const removed = queue.pop()!;
    result.delete(removed);
    for (const permission of catalog) {
      if (permission.requires === removed && result.has(permission.key)) {
        queue.push(permission.key);
      }
    }
  }

  return [...result];
}

/** Sets a whole feature on or off, as the screen, section and module checkboxes do. */
export function setMany(keys: readonly string[], targets: PermissionInfo[], checked: boolean, catalog: PermissionInfo[]): string[] {
  let result = [...keys];
  for (const permission of targets.filter(isSelectable)) {
    result = toggleKey(result, permission.key, checked, catalog);
  }
  return result;
}

/** The scope level a role holds in a selector: the highest ticked one. */
export function scopeLevel(feature: TreeFeature, effective: ReadonlySet<string>): PermissionInfo {
  const held = feature.permissions.filter((permission) => isEnabled(permission, effective));
  return held[held.length - 1] ?? feature.permissions[0];
}

/** Picks a level in a selector: every other level is cleared and the chosen one is ticked. */
export function setScope(keys: readonly string[], feature: TreeFeature, level: PermissionInfo, catalog: PermissionInfo[]): string[] {
  const groupKeys = new Set(feature.permissions.map((permission) => permission.key));
  const cleared = keys.filter((key) => !groupKeys.has(key));
  return isSelectable(level) ? toggleKey(cleared, level.key, true, catalog) : cleared;
}

/** The permissions that match a search, with their module, screen and section names also searched. */
export function matches(permission: PermissionInfo, term: string): boolean {
  const needle = term.trim().toLowerCase();
  if (needle === '') {
    return true;
  }

  return [permission.name, permission.description, permission.module, permission.screen, permission.section, permission.action]
    .some((text) => text.toLowerCase().includes(needle));
}

/** How many ticks differ from the stored state, for the "N unsaved changes" bar. */
export function changeCount(before: readonly string[], after: readonly string[]): number {
  const left = new Set(before);
  const right = new Set(after);
  let changes = 0;
  for (const key of left) {
    if (!right.has(key)) {
      changes++;
    }
  }
  for (const key of right) {
    if (!left.has(key)) {
      changes++;
    }
  }
  return changes;
}
