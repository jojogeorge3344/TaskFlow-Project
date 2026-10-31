const SYSTEM_BADGES = new Set(['administrator', 'manager', 'employee']);

/** Badge classes for a role name: the system roles keep their own colors, every custom role shares one style. */
export function roleBadgeClass(role: string): string {
  const key = role.toLowerCase();
  return `tf-badge tf-badge--${SYSTEM_BADGES.has(key) ? key : 'custom'}`;
}
