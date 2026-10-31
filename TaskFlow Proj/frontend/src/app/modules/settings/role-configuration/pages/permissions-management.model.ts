import { PermissionInfo } from '../models/role.model';
import { CheckState, TreeFeature } from '../utils/permission-tree';

/** What the right-hand pane shows: an existing role by id, or a role that has not been saved yet. */
export type Selection = number | 'new' | null;

export type EnabledFilter = 'all' | 'enabled' | 'disabled';

export type PermissionView = 'tree' | 'matrix';

/** A module as the navigator and panel show it, after the search and the enabled filter have been applied. */
export interface ModuleView {
  name: string;
  screens: ScreenView[];
  enabled: number;
  total: number;
  state: CheckState;
  selectable: boolean;
}

export interface ScreenView {
  name: string;
  key: string;
  sections: { name: string; features: FeatureView[] }[];
  enabled: number;
  total: number;
  state: CheckState;
  selectable: boolean;
}

export interface FeatureView {
  feature: TreeFeature;
  state: CheckState;
  level: PermissionInfo;
}
