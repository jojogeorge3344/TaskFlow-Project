import { User } from '../../core/models';

export type StatusFilter = '' | 'active' | 'inactive';

export type Panel = { mode: 'create' } | { mode: 'edit'; employee: User } | { mode: 'details'; employee: User };
