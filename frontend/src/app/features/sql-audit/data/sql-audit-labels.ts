import {
  AccountLevel,
  RiskLevel,
  Securable,
  SqlAccessItem,
  SqlAccountHistoryEntry,
  TlsMode,
  Unavailable,
  ValidationStatus,
} from './sql-audit.models';

/** Spanish labels of the normalized values of the SQL audit (SPEC-0005). Unknown values are shown as received. */
const LABELS = {
  engine: {
    sqlserver: 'SQL Server',
    postgresql: 'PostgreSQL',
    mysql: 'MySQL',
    mariadb: 'MariaDB',
  },
  tls: {
    verify: 'Certificado verificado',
    pinned: 'Certificado fijado',
    unverified: 'Certificado sin verificar',
  },
  level: { server: 'Motor', database: 'Base de datos' },
  accountType: {
    'sql-login': 'Login SQL',
    'windows-login': 'Login de Windows',
    'windows-group': 'Grupo de Windows',
    'entra-login': 'Login de Microsoft Entra ID',
    'server-role': 'Rol de servidor',
    'database-user': 'Usuario de base de datos',
    'database-role': 'Rol de base de datos',
    'application-role': 'Rol de aplicación',
    'user-without-login': 'Usuario sin login',
    'pg-role': 'Rol de PostgreSQL',
    'mysql-account': 'Cuenta usuario@host',
    'mysql-role': 'Rol de MySQL/MariaDB',
    certificate: 'Certificado',
    other: 'Otro',
  },
  objectType: {
    table: 'Tabla',
    view: 'Vista',
    'materialized-view': 'Vista materializada',
    'foreign-table': 'Tabla externa',
    procedure: 'Procedimiento',
    function: 'Función',
    aggregate: 'Agregado',
    sequence: 'Secuencia',
    type: 'Tipo',
    domain: 'Dominio',
    synonym: 'Sinónimo',
    schema: 'Esquema',
    database: 'Base de datos',
    language: 'Lenguaje',
    'large-object': 'Objeto grande',
    'foreign-data-wrapper': 'Contenedor de datos externos',
    'foreign-server': 'Servidor externo',
    tablespace: 'Espacio de tablas',
    parameter: 'Parámetro',
    assembly: 'Ensamblado',
    'xml-schema-collection': 'Colección de esquemas XML',
    certificate: 'Certificado',
    'asymmetric-key': 'Clave asimétrica',
    'symmetric-key': 'Clave simétrica',
    'full-text-catalog': 'Catálogo de texto completo',
    'service-broker': 'Service Broker',
    login: 'Login',
    'server-role': 'Rol de servidor',
    'database-role': 'Rol de base de datos',
    'application-role': 'Rol de aplicación',
    user: 'Usuario',
    endpoint: 'Punto de conexión',
    'availability-group': 'Grupo de disponibilidad',
    event: 'Evento',
    other: 'Otro',
  },
  securableClass: {
    server: 'Motor',
    database: 'Base de datos',
    schema: 'Esquema',
    object: 'Objeto',
    column: 'Columna',
    'default-privileges': 'Privilegios por defecto',
  },
  state: {
    grant: 'Concedido',
    'grant-with-grant-option': 'Concedido con opción de conceder',
    deny: 'Denegado',
  },
  risk: { critical: 'Crítico', high: 'Alto', general: 'General', denied: 'Denegado' },
  validation: {
    pending: 'Pendiente',
    valid: 'Válido',
    changed: 'Cambió desde la validación',
    removed: 'Eliminado del motor',
  },
  reason: {
    'connection-failed': 'No se pudo conectar',
    'certificate-invalid': 'Certificado no válido',
    'authentication-failed': 'Error de autenticación',
    'permission-denied': 'Permiso denegado',
    offline: 'Fuera de línea',
    timeout: 'Tiempo agotado',
    error: 'Error',
  },
  databaseState: {
    online: 'En línea',
    offline: 'Fuera de línea',
    restoring: 'Restaurando',
    recovering: 'Recuperando',
    suspect: 'Sospechosa',
    emergency: 'Emergencia',
    unknown: 'Desconocido',
  },
  authenticationMode: {
    mixed: 'Mixto (SQL y Windows)',
    windows: 'Solo Windows',
    password: 'Contraseña',
    unknown: 'Desconocido',
  },
  changeType: {
    added: 'Añadido',
    removed: 'Eliminado del motor',
    changed: 'Cambió',
    validated: 'Marcado como válido',
    unvalidated: 'Desmarcado como válido',
  },
  changeTarget: { account: 'Cuenta', 'role-membership': 'Rol', permission: 'Permiso' },
  changeField: {
    enabled: 'Habilitada',
    locked: 'Bloqueada',
    type: 'Tipo',
    defaultDatabase: 'Base de datos por defecto',
    defaultSchema: 'Esquema por defecto',
    login: 'Login',
    isOrphaned: 'Huérfano',
    createdAt: 'Fecha de creación',
    'password.policyEnforced': 'Política de contraseñas',
    'password.expirationEnforced': 'Caducidad de la contraseña',
    'password.expired': 'Contraseña vencida',
    'password.validUntil': 'Contraseña válida hasta',
    grantor: 'Concedido por',
  },
} satisfies Record<string, Record<string, string>>;

export type SqlLabelKind = keyof typeof LABELS;

export function sqlLabel(kind: SqlLabelKind, value: string | null | undefined): string {
  if (value === null || value === undefined || value === '') {
    return 'No disponible';
  }
  return (LABELS[kind] as Record<string, string>)[value] ?? value;
}

/** Options of a `mat-select` filter: the values present, with their label, sorted by label. */
export function options(
  kind: SqlLabelKind,
  values: readonly string[],
): { value: string; label: string }[] {
  return values
    .map((value) => ({ value, label: sqlLabel(kind, value) }))
    .sort((a, b) => a.label.localeCompare(b.label, 'es', { sensitivity: 'base' }));
}

/** "Sí" / "No", or "No disponible" when the engine does not report it (RN-7). */
export function yesNo(value: boolean | null | undefined): string {
  return value === null || value === undefined ? 'No disponible' : value ? 'Sí' : 'No';
}

/** Level column of the accounts (RF-21): "Motor", or the name of the database. */
export function levelLabel(level: AccountLevel, database: string | null): string {
  return level === 'server' ? 'Motor' : (database ?? 'Base de datos');
}

/**
 * Account name as the engine knows it: `usuario@host` in MySQL and MariaDB. An anonymous MySQL/MariaDB account
 * (`''@host`) has an empty name and is shown as "(anónima)".
 */
export function accountName(account: { name: string; host: string | null }): string {
  const name = account.name === '' ? '(anónima)' : account.name;
  return account.host ? `${name}@${account.host}` : name;
}

/** Engine and version: "SQL Server 16.0.4135". */
export function engineLabel(engine: string, version: string | null): string {
  return version ? `${sqlLabel('engine', engine)} ${version}` : sqlLabel('engine', engine);
}

/** Connection target: `host\instancia:puerto` with the parts that apply. */
export function destination(target: {
  host: string;
  port: number | null;
  instanceName: string | null;
}): string {
  return (
    target.host +
    (target.instanceName ? `\\${target.instanceName}` : '') +
    (target.port ? `:${target.port}` : '')
  );
}

/** Securable path `base.esquema.objeto.columna` (RF-22); "Motor" for server-level permissions. */
export function securablePath(securable: Securable | null): string {
  if (!securable) {
    return '—';
  }
  const parts = [securable.database, securable.schema, securable.name, securable.column].filter(
    (part): part is string => !!part,
  );
  return parts.length > 0 ? parts.join('.') : 'Motor';
}

/** Kind of securable under its path: "Tabla", "Columna de Vista", "Privilegios por defecto (Tabla)"… */
export function securableKind(securable: Securable | null): string {
  if (!securable) {
    return '';
  }
  const objectType = securable.objectType ? sqlLabel('objectType', securable.objectType) : null;
  switch (securable.class) {
    case 'object':
      return objectType ?? sqlLabel('securableClass', securable.class);
    case 'column':
      return objectType ? `Columna de ${objectType.toLocaleLowerCase('es')}` : 'Columna';
    case 'default-privileges':
      return objectType ? `Privilegios por defecto (${objectType})` : 'Privilegios por defecto';
    case 'server':
      // A server-level permission without an object already reads "Motor" in its path.
      return objectType ?? '';
    default:
      return sqlLabel('securableClass', securable.class);
  }
}

/** Name of a section of the instance (RF-7): "Instancia", "Motor" or "Base de datos <name>". */
export function sectionLabel(section: string): string {
  if (section === 'instance') {
    return 'Instancia';
  }
  if (section === 'server') {
    return 'Motor';
  }
  return section.startsWith('database:') ? `Base de datos ${section.slice(9)}` : section;
}

/** Message of a part that could not be read in the last collection (§8.4, RF-13). */
export function unavailableMessage(item: Unavailable): string {
  const reason = sqlLabel('reason', item.reason).toLocaleLowerCase('es');
  return `${sectionLabel(item.section)}: No se pudo leer en la última recolección: ${reason}.`;
}

/** Value of a changed field in the change log: booleans as "Sí"/"No", types with their label. */
export function changeValue(field: string, value: string | null): string {
  if (value === null || value === '') {
    return 'Sin valor';
  }
  if (value === 'true' || value === 'false') {
    return value === 'true' ? 'Sí' : 'No';
  }
  return field === 'type' ? sqlLabel('accountType', value) : value;
}

/** Element of a change log entry: "Cuenta", "Rol sysadmin" or "SELECT (Concedido) en Ventas.dbo.Facturas". */
export function historyElement(entry: SqlAccountHistoryEntry): string {
  const item = entry.item;
  if (entry.target === 'account' || !item) {
    return 'Cuenta';
  }
  if (item.kind === 'role-membership') {
    return `Rol ${item.role ?? ''}`.trim();
  }
  const state = item.state ? ` (${sqlLabel('state', item.state)})` : '';
  return `${item.permission ?? ''}${state} en ${securablePath(item.securable)}`;
}

/**
 * Text that identifies a role membership or a permission, e.g. for the checkbox label: "rol sysadmin" or
 * "SELECT (Concedido) en Ventas.dbo.Facturas", followed by the account when it is known ("de app_ventas").
 */
export function accessElement(item: SqlAccessItem): string {
  const text =
    item.kind === 'role-membership'
      ? `rol ${item.role?.name ?? ''}`.trim()
      : `${item.permission ?? ''}${item.state ? ` (${sqlLabel('state', item.state)})` : ''} en ${securablePath(item.securable)}`;
  return item.account ? `${text} de ${accountName(item.account)}` : text;
}

/** MD3 colour roles allowed for states (§11.1). Applied to the icon and the text only. */
export type StatusTone = 'error' | 'tertiary' | 'primary' | 'on-surface-variant' | 'outline';

/** A state rendered as icon + text: its meaning never depends on colour alone (RNF-7, CA-18). */
export interface StatusPresentation {
  readonly label: string;
  readonly icon: string;
  readonly tone: StatusTone;
}

const RISK: Record<RiskLevel, StatusPresentation> = {
  critical: { label: 'Crítico', icon: 'error', tone: 'error' },
  high: { label: 'Alto', icon: 'warning', tone: 'tertiary' },
  general: { label: 'General', icon: 'info', tone: 'on-surface-variant' },
  denied: { label: 'Denegado', icon: 'block', tone: 'on-surface-variant' },
};

const VALIDATION: Record<ValidationStatus, StatusPresentation> = {
  valid: { label: 'Válido', icon: 'check_circle', tone: 'primary' },
  pending: { label: 'Pendiente', icon: 'pending', tone: 'on-surface-variant' },
  changed: { label: 'Cambió desde la validación', icon: 'change_circle', tone: 'tertiary' },
  removed: { label: 'Eliminado del motor', icon: 'remove_circle_outline', tone: 'outline' },
};

export function riskStatus(risk: RiskLevel): StatusPresentation {
  return RISK[risk] ?? RISK.high;
}

export function validationStatus(status: ValidationStatus): StatusPresentation {
  return VALIDATION[status] ?? VALIDATION.pending;
}

export function tlsStatus(tls: TlsMode): StatusPresentation | null {
  return tls === 'unverified'
    ? { label: 'Certificado sin verificar', icon: 'gpp_maybe', tone: 'tertiary' }
    : null;
}

export const UNAVAILABLE_STATUS: StatusPresentation = {
  label: 'Datos no disponibles',
  icon: 'warning',
  tone: 'tertiary',
};

export const STALE_STATUS: StatusPresentation = {
  label: 'Sin actualizar',
  icon: 'warning',
  tone: 'tertiary',
};

export const DELETED_DEVICE_STATUS: StatusPresentation = {
  label: 'Equipo eliminado',
  icon: 'delete',
  tone: 'on-surface-variant',
};
