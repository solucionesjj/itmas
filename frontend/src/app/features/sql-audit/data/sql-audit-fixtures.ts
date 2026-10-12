import {
  Paged,
  SqlAccessItem,
  SqlAccountDetail,
  SqlAccountHistoryEntry,
  SqlAccountListItem,
  SqlInstanceDetail,
  SqlInstanceListItem,
} from './sql-audit.models';

/** Test data of the SQL audit feature (SPEC-0005 §12.1). Not used by the application. */

export function pageOf<T>(items: readonly T[]): Paged<T> {
  return { items, page: 1, pageSize: 20, total: items.length };
}

export function instanceOf(overrides: Partial<SqlInstanceListItem> = {}): SqlInstanceListItem {
  return {
    id: 'i1',
    device: { id: 'd1', name: 'srv-db-01', status: 'active' },
    alias: 'ventas',
    engine: 'sqlserver',
    host: 'srv-db-01',
    port: 1433,
    instanceName: null,
    version: '16.0.4135',
    tls: 'verify',
    lastCollectedAt: '2026-10-11T06:00:00Z',
    accountCount: 12,
    pendingCount: 5,
    pendingCriticalCount: 1,
    hasUnavailable: false,
    ...overrides,
  };
}

export function instanceDetailOf(overrides: Partial<SqlInstanceDetail> = {}): SqlInstanceDetail {
  return {
    ...instanceOf(),
    edition: 'Standard Edition',
    authenticationMode: 'mixed',
    lastReceivedAt: '2026-10-11T06:00:05Z',
    databases: [
      { name: 'Ventas', state: 'online', owner: 'sa' },
      { name: 'Compras', state: 'offline', owner: null },
    ],
    unavailable: [],
    omittedBuiltinPermissions: 340,
    accountTypes: ['sql-login', 'database-user'],
    objectTypes: ['table', 'procedure'],
    ...overrides,
  };
}

export function accountOf(overrides: Partial<SqlAccountListItem> = {}): SqlAccountListItem {
  return {
    id: 'a1',
    level: 'server',
    database: null,
    name: 'app_ventas',
    host: null,
    type: 'sql-login',
    enabled: true,
    locked: false,
    risk: 'critical',
    riskInherited: true,
    roles: ['sysadmin'],
    pendingCount: 3,
    validation: { status: 'pending', by: null, at: null },
    fingerprint: 'f'.repeat(64),
    firstSeenAt: '2026-10-01T06:00:00Z',
    removedAt: null,
    stale: false,
    ...overrides,
  };
}

export function accountDetailOf(overrides: Partial<SqlAccountDetail> = {}): SqlAccountDetail {
  return {
    ...accountOf(),
    instance: {
      id: 'i1',
      alias: 'ventas',
      engine: 'sqlserver',
      host: 'srv-db-01',
      port: 1433,
      instanceName: null,
      tls: 'verify',
      device: { id: 'd1', name: 'srv-db-01', status: 'active' },
    },
    ownRisk: 'general',
    inheritedRisk: 'critical',
    createdAt: '2025-01-10T10:00:00Z',
    modifiedAt: null,
    defaultDatabase: 'Ventas',
    defaultSchema: null,
    login: null,
    isOrphaned: false,
    password: { policyEnforced: true, expirationEnforced: false, expired: false, validUntil: null },
    lastSeenAt: '2026-10-11T06:00:00Z',
    databases: ['Ventas'],
    objectTypes: ['table'],
    ...overrides,
  };
}

export function permissionOf(overrides: Partial<SqlAccessItem> = {}): SqlAccessItem {
  return {
    id: 'p1',
    kind: 'permission',
    account: null,
    securable: {
      class: 'object',
      database: 'Ventas',
      schema: 'dbo',
      name: 'Facturas',
      objectType: 'table',
      column: null,
    },
    role: null,
    permission: 'DELETE',
    state: 'grant',
    grantor: 'dbo',
    risk: 'high',
    validation: { status: 'pending', by: null, at: null },
    firstSeenAt: '2026-10-01T06:00:00Z',
    removedAt: null,
    stale: false,
    ...overrides,
  };
}

export function roleMembershipOf(overrides: Partial<SqlAccessItem> = {}): SqlAccessItem {
  return permissionOf({
    id: 'm1',
    kind: 'role-membership',
    securable: null,
    role: { accountId: 'a9', name: 'sysadmin' },
    permission: null,
    state: null,
    grantor: null,
    risk: 'critical',
    ...overrides,
  });
}

export function historyEntryOf(
  overrides: Partial<SqlAccountHistoryEntry> = {},
): SqlAccountHistoryEntry {
  return {
    at: '2026-10-11T06:00:00Z',
    type: 'changed',
    target: 'account',
    item: null,
    changes: [{ field: 'enabled', before: 'true', after: 'false' }],
    by: null,
    ...overrides,
  };
}

/**
 * Visible text of an element with its text nodes separated by spaces (the layout separates them with gaps or blocks,
 * not with whitespace). Icon ligatures are skipped unless `icons` is true.
 */
export function readable(element: Element, icons = false): string {
  const walker = element.ownerDocument.createTreeWalker(element, NodeFilter.SHOW_TEXT);
  const parts: string[] = [];
  for (let node = walker.nextNode(); node; node = walker.nextNode()) {
    if (icons || !node.parentElement?.closest('mat-icon')) {
      parts.push(node.textContent ?? '');
    }
  }
  return parts.join(' ').replace(/\s+/g, ' ').trim();
}

/** Text of each table cell as assistive technologies read it: icons are aria-hidden ligatures and are skipped. */
export function rowTexts(element: Element): string[][] {
  return [...element.querySelectorAll('tr.mat-mdc-row')].map((row) =>
    [...row.querySelectorAll('td')].map((cell) => readable(cell)),
  );
}

/** Ligatures of the icons shown inside `element`, in document order. */
export function iconNames(element: Element): (string | undefined)[] {
  return [...element.querySelectorAll('mat-icon')].map((icon) => icon.textContent?.trim());
}
