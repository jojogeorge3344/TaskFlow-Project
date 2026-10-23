import { PermissionInfo } from '../../core/models';
import {
  buildTree,
  changeCount,
  effectiveKeys,
  featuresOf,
  matches,
  scopeLevel,
  setMany,
  setScope,
  stateOf,
  toggleKey
} from './permission-tree';

function perm(key: string, module: string, screen: string, extra: Partial<PermissionInfo> = {}): PermissionInfo {
  return {
    id: 1,
    key,
    name: key,
    description: `About ${key}`,
    group: module,
    isAssignable: true,
    module,
    screen,
    section: '',
    action: 'View',
    requires: null,
    scopeGroup: null,
    scopeRank: 0,
    alwaysGranted: false,
    ...extra
  };
}

const CATALOG: PermissionInfo[] = [
  perm('dashboard.view.own', 'Dashboard', 'Dashboard', { scopeGroup: 'dashboard.view', scopeRank: 1, alwaysGranted: true, isAssignable: false }),
  perm('dashboard.view.team', 'Dashboard', 'Dashboard', { scopeGroup: 'dashboard.view', scopeRank: 2 }),
  perm('dashboard.view.organization', 'Dashboard', 'Dashboard', { scopeGroup: 'dashboard.view', scopeRank: 3 }),
  perm('projects.view', 'Projects', 'Project list'),
  perm('projects.create', 'Projects', 'Project list', { requires: 'projects.view', action: 'Create' }),
  perm('projects.delete', 'Projects', 'Project list', { requires: 'projects.view', action: 'Delete' }),
  perm('roles.manage', 'Role configuration', 'Role configuration', { isAssignable: false })
];

describe('permission tree', () => {
  it('lists every module once, however many roles use it', () => {
    const tree = buildTree([...CATALOG, ...CATALOG.slice(3, 5)].filter((permission, index, all) => all.findIndex((other) => other.key === permission.key) === index));

    expect(tree.map((module) => module.name)).toEqual(['Dashboard', 'Projects', 'Role configuration']);
  });

  it('groups the levels of a scope selector into one feature, lowest first', () => {
    const dashboard = buildTree(CATALOG)[0];
    const features = featuresOf(dashboard);

    expect(features.length).toBe(1);
    expect(features[0].isScope).toBeTrue();
    expect(features[0].permissions.map((permission) => permission.key)).toEqual([
      'dashboard.view.own',
      'dashboard.view.team',
      'dashboard.view.organization'
    ]);
  });

  it('adds the lower levels of a scope to what a role holds', () => {
    const effective = effectiveKeys(['dashboard.view.organization'], CATALOG);

    expect(effective.has('dashboard.view.team')).toBeTrue();
    expect(effective.has('dashboard.view.own')).toBeTrue();
  });

  it('ticking an action ticks the view it needs; clearing the view clears the actions', () => {
    const ticked = toggleKey([], 'projects.create', true, CATALOG);
    expect(ticked.sort()).toEqual(['projects.create', 'projects.view']);

    const cleared = toggleKey([...ticked, 'projects.delete'], 'projects.view', false, CATALOG);
    expect(cleared).toEqual([]);
  });

  it('never ticks something an administrator cannot assign', () => {
    expect(toggleKey([], 'roles.manage', true, CATALOG)).toEqual([]);
    expect(toggleKey([], 'dashboard.view.own', true, CATALOG)).toEqual([]);
  });

  it('sets a whole group on and off', () => {
    const projects = buildTree(CATALOG)[1];
    const targets = featuresOf(projects).flatMap((feature) => feature.permissions);

    const on = setMany([], targets, true, CATALOG);
    expect(on.sort()).toEqual(['projects.create', 'projects.delete', 'projects.view']);
    expect(stateOf(targets, effectiveKeys(on, CATALOG))).toBe('all');

    const some = setMany([], [targets[0]], true, CATALOG);
    expect(stateOf(targets, effectiveKeys(some, CATALOG))).toBe('some');
    expect(setMany(on, targets, false, CATALOG)).toEqual([]);
  });

  it('picks one scope level at a time', () => {
    const feature = featuresOf(buildTree(CATALOG)[0])[0];
    const [own, team, organization] = feature.permissions;

    const toTeam = setScope(['dashboard.view.organization', 'projects.view'], feature, team, CATALOG);
    expect(toTeam.sort()).toEqual(['dashboard.view.team', 'projects.view']);
    expect(scopeLevel(feature, effectiveKeys(toTeam, CATALOG)).key).toBe('dashboard.view.team');
    expect(scopeLevel(feature, effectiveKeys(setScope(toTeam, feature, organization, CATALOG), CATALOG)).key).toBe('dashboard.view.organization');
    expect(setScope(toTeam, feature, own, CATALOG)).toEqual(['projects.view']);
  });

  it('matches a search on the name, description, module, screen and action', () => {
    const create = CATALOG.find((permission) => permission.key === 'projects.create')!;

    expect(matches(create, 'CREATE')).toBeTrue();
    expect(matches(create, 'project list')).toBeTrue();
    expect(matches(create, 'nothing like it')).toBeFalse();
    expect(matches(create, '   ')).toBeTrue();
  });

  it('counts the ticks that differ from the stored state', () => {
    expect(changeCount(['a', 'b'], ['b', 'c'])).toBe(2);
    expect(changeCount(['a'], ['a'])).toBe(0);
  });
});
