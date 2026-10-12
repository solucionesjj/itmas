/**
 * Contracts of the SQL audit API (SPEC-0005 §12.1). Every name, path or value that comes from a database engine is
 * untrusted (RNF-8): always render it as text, never as HTML (CA-17).
 */

export type SqlEngine = 'sqlserver' | 'postgresql' | 'mysql' | 'mariadb';
export type TlsMode = 'verify' | 'pinned' | 'unverified';
/** `denied` only applies to permissions whose state is `deny`. */
export type RiskLevel = 'critical' | 'high' | 'general' | 'denied';
export type AccountRisk = Exclude<RiskLevel, 'denied'>;
/** `changed` only applies to accounts. */
export type ValidationStatus = 'pending' | 'valid' | 'changed' | 'removed';
export type AccountLevel = 'server' | 'database';
export type AccessKind = 'role-membership' | 'permission';
export type PermissionState = 'grant' | 'grant-with-grant-option' | 'deny';
export type SecurableClass =
  'server' | 'database' | 'schema' | 'object' | 'column' | 'default-privileges';

export interface Paged<T> {
  readonly items: readonly T[];
  readonly page: number;
  readonly pageSize: number;
  readonly total: number;
}

export interface Validation {
  readonly status: ValidationStatus;
  readonly by: string | null;
  readonly at: string | null;
}

export interface DeviceRef {
  readonly id: string;
  readonly name: string;
  readonly status: 'pending' | 'active' | 'deleted';
}

/** A part of the instance that could not be read: `instance`, `server` or `database:<name>` (RF-7, RF-13). */
export interface Unavailable {
  readonly section: string;
  readonly reason: string;
}

export interface Securable {
  readonly class: SecurableClass;
  readonly database: string | null;
  readonly schema: string | null;
  readonly name: string | null;
  readonly objectType: string | null;
  readonly column: string | null;
}

export interface SqlInstanceListItem {
  readonly id: string;
  readonly device: DeviceRef;
  readonly alias: string;
  readonly engine: SqlEngine;
  readonly host: string;
  readonly port: number | null;
  readonly instanceName: string | null;
  readonly version: string | null;
  readonly tls: TlsMode;
  readonly lastCollectedAt: string | null;
  readonly accountCount: number;
  readonly pendingCount: number;
  readonly pendingCriticalCount: number;
  readonly hasUnavailable: boolean;
}

export interface SqlDatabase {
  readonly name: string;
  readonly state: string;
  readonly owner: string | null;
}

export interface SqlInstanceDetail extends SqlInstanceListItem {
  readonly edition: string | null;
  readonly authenticationMode: string | null;
  readonly lastReceivedAt: string | null;
  readonly databases: readonly SqlDatabase[];
  readonly unavailable: readonly Unavailable[];
  readonly omittedBuiltinPermissions: number;
  /** Account types present, for the filter. */
  readonly accountTypes: readonly string[];
  /** Object types present in the permissions, for the filter. */
  readonly objectTypes: readonly string[];
}

export interface SqlAccountListItem {
  readonly id: string;
  readonly level: AccountLevel;
  readonly database: string | null;
  readonly name: string;
  readonly host: string | null;
  readonly type: string;
  readonly enabled: boolean;
  readonly locked: boolean;
  /** Effective risk: the highest of the account, its elements and what it inherits from its roles. */
  readonly risk: AccountRisk;
  /** The effective risk comes from the roles the account is a member of (P-24). */
  readonly riskInherited: boolean;
  readonly roles: readonly string[];
  /** Current elements not valid: the account (unless `valid`), its roles and its permissions. */
  readonly pendingCount: number;
  readonly validation: Validation;
  readonly fingerprint: string;
  readonly firstSeenAt: string;
  readonly removedAt: string | null;
  /** Its section could not be read in the last collection (RF-13). */
  readonly stale: boolean;
}

export interface SqlInstanceRef {
  readonly id: string;
  readonly alias: string;
  readonly engine: SqlEngine;
  readonly host: string;
  readonly port: number | null;
  readonly instanceName: string | null;
  readonly tls: TlsMode;
  readonly device: DeviceRef;
}

export interface PasswordAttributes {
  readonly policyEnforced: boolean | null;
  readonly expirationEnforced: boolean | null;
  readonly expired: boolean | null;
  readonly validUntil: string | null;
}

export interface SqlAccountDetail extends SqlAccountListItem {
  readonly instance: SqlInstanceRef;
  readonly ownRisk: AccountRisk;
  readonly inheritedRisk: AccountRisk | null;
  readonly createdAt: string | null;
  readonly modifiedAt: string | null;
  readonly defaultDatabase: string | null;
  readonly defaultSchema: string | null;
  readonly login: string | null;
  readonly isOrphaned: boolean;
  readonly password: PasswordAttributes | null;
  readonly lastSeenAt: string;
  /** Databases present in its permissions, for the filter. */
  readonly databases: readonly string[];
  readonly objectTypes: readonly string[];
}

export interface SqlAccessAccount {
  readonly id: string;
  readonly name: string;
  readonly level: AccountLevel;
  readonly database: string | null;
  readonly host: string | null;
  readonly type: string;
}

export interface SqlAccessItem {
  readonly id: string;
  readonly kind: AccessKind;
  /** Filled in the permissions of an instance; null in the elements of an account. */
  readonly account: SqlAccessAccount | null;
  readonly securable: Securable | null;
  readonly role: { readonly accountId: string; readonly name: string } | null;
  readonly permission: string | null;
  readonly state: PermissionState | null;
  readonly grantor: string | null;
  readonly risk: RiskLevel;
  readonly validation: Validation;
  readonly firstSeenAt: string;
  readonly removedAt: string | null;
  readonly stale: boolean;
}

export type HistoryEntryType = 'added' | 'removed' | 'changed' | 'validated' | 'unvalidated';
export type HistoryTarget = 'account' | 'role-membership' | 'permission';

export interface SqlAccountHistoryEntry {
  readonly at: string;
  readonly type: HistoryEntryType;
  readonly target: HistoryTarget;
  readonly item: {
    readonly kind: AccessKind;
    readonly securable: Securable | null;
    readonly role: string | null;
    readonly permission: string | null;
    readonly state: string | null;
  } | null;
  /** Only in `changed`. */
  readonly changes: readonly {
    readonly field: string;
    readonly before: string | null;
    readonly after: string | null;
  }[];
  /** Person who validated or unvalidated; null in detected changes. */
  readonly by: string | null;
}

// Query parameters. Empty strings mean "no filter" and are not sent.

export type InstanceSort = 'alias' | 'lastCollectedAt' | 'pending' | 'pendingCritical';
export type AccountSort = 'name' | 'risk' | 'pending' | 'firstSeenAt';
export type PermissionSort = 'risk' | 'account' | 'securable' | 'permission' | 'firstSeenAt';
export type AccountValidationFilter = ValidationStatus | '';
export type ItemValidationFilter = Exclude<ValidationStatus, 'changed'> | '';

export interface Sorting<T extends string> {
  readonly active: T;
  readonly descending: boolean;
}

export interface InstanceQuery {
  readonly search: string;
  readonly engine: SqlEngine | '';
  readonly sort: Sorting<InstanceSort>;
  readonly page: number;
  readonly pageSize: number;
}

export interface AccountQuery {
  readonly level: AccountLevel | '';
  readonly database: string;
  readonly type: string;
  readonly risk: AccountRisk | '';
  readonly validation: AccountValidationFilter;
  readonly search: string;
  readonly sort: Sorting<AccountSort>;
  readonly page: number;
  readonly pageSize: number;
}

export interface PermissionQuery {
  readonly risk: RiskLevel | '';
  readonly validation: ItemValidationFilter;
  readonly database: string;
  readonly account: string;
  readonly objectType: string;
  readonly search: string;
  readonly sort: Sorting<PermissionSort>;
  readonly page: number;
  readonly pageSize: number;
}

export interface AccountItemQuery {
  readonly kind: AccessKind;
  readonly risk: RiskLevel | '';
  readonly validation: ItemValidationFilter;
  readonly database: string;
  readonly objectType: string;
  readonly page: number;
  readonly pageSize: number;
}
