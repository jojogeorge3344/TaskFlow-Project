import { User } from '../models/user.model';

export type StatusFilter = '' | 'active' | 'inactive';

export type Panel = { mode: 'create' } | { mode: 'edit'; employee: User } | { mode: 'details'; employee: User };
