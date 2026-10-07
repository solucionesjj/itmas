import { Permission } from './authorization.models';

export interface PermissionGroup {
  readonly category: string;
  readonly permissions: readonly Permission[];
}

/** Groups permissions by category (alphabetically), keeping the catalog order inside each group. */
export function groupByCategory(permissions: readonly Permission[]): PermissionGroup[] {
  const groups = new Map<string, Permission[]>();
  for (const permission of permissions) {
    groups.set(permission.category, [...(groups.get(permission.category) ?? []), permission]);
  }
  return [...groups.entries()]
    .sort(([a], [b]) => a.localeCompare(b, 'es'))
    .map(([category, items]) => ({ category, permissions: items }));
}
