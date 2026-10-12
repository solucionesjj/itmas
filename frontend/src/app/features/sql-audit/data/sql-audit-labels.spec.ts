import { historyEntryOf, permissionOf, roleMembershipOf } from './sql-audit-fixtures';
import {
  accessElement,
  accountName,
  changeValue,
  destination,
  engineLabel,
  historyElement,
  levelLabel,
  options,
  riskStatus,
  securableKind,
  securablePath,
  sqlLabel,
  tlsStatus,
  unavailableMessage,
  validationStatus,
  yesNo,
} from './sql-audit-labels';

describe('SQL audit labels', () => {
  it('translates the normalized values', () => {
    expect(sqlLabel('engine', 'postgresql')).toBe('PostgreSQL');
    expect(sqlLabel('accountType', 'sql-login')).toBe('Login SQL');
    expect(sqlLabel('accountType', 'database-role')).toBe('Rol de base de datos');
    expect(sqlLabel('objectType', 'materialized-view')).toBe('Vista materializada');
    expect(sqlLabel('securableClass', 'default-privileges')).toBe('Privilegios por defecto');
    expect(sqlLabel('reason', 'certificate-invalid')).toBe('Certificado no válido');
    expect(sqlLabel('changeField', 'password.expired')).toBe('Contraseña vencida');
  });

  it('names the permission states as the definition does', () => {
    expect(sqlLabel('state', 'grant')).toBe('Concedido');
    expect(sqlLabel('state', 'grant-with-grant-option')).toBe('Concedido con opción de conceder');
    expect(sqlLabel('state', 'deny')).toBe('Denegado');
  });

  it('never invents a value (RN-7) and shows unknown values as received', () => {
    expect(sqlLabel('authenticationMode', null)).toBe('No disponible');
    expect(sqlLabel('objectType', 'new-kind')).toBe('new-kind');
    expect(yesNo(null)).toBe('No disponible');
    expect(yesNo(true)).toBe('Sí');
  });

  it('builds the filter options sorted by label', () => {
    expect(options('objectType', ['view', 'table']).map((option) => option.label)).toEqual([
      'Tabla',
      'Vista',
    ]);
  });

  it('formats levels, names, engines and targets', () => {
    expect(levelLabel('server', null)).toBe('Motor');
    expect(levelLabel('database', 'Ventas')).toBe('Ventas');
    expect(accountName({ name: 'app', host: '%' })).toBe('app@%');
    expect(accountName({ name: 'app', host: null })).toBe('app');
    expect(accountName({ name: '', host: 'localhost' })).toBe('(anónima)@localhost');
    expect(engineLabel('sqlserver', '16.0')).toBe('SQL Server 16.0');
    expect(destination({ host: 'db', port: null, instanceName: 'PROD' })).toBe('db\\PROD');
    expect(destination({ host: 'db', port: 5432, instanceName: null })).toBe('db:5432');
  });

  it('RF-22: shows the securable as base.esquema.objeto.columna', () => {
    expect(
      securablePath({
        class: 'column',
        database: 'Ventas',
        schema: 'dbo',
        name: 'Clientes',
        objectType: 'table',
        column: 'Email',
      }),
    ).toBe('Ventas.dbo.Clientes.Email');
    expect(
      securablePath({
        class: 'object',
        database: 'tienda',
        schema: null,
        name: 'pedidos',
        objectType: 'table',
        column: null,
      }),
    ).toBe('tienda.pedidos');
    const server = {
      class: 'server' as const,
      database: null,
      schema: null,
      name: null,
      objectType: null,
      column: null,
    };
    expect(securablePath(server)).toBe('Motor');
    expect(securableKind(server)).toBe('');
    expect(securableKind({ ...server, name: 'ventas_app', objectType: 'login' })).toBe('Login');
    expect(securableKind({ ...server, class: 'column', objectType: 'view' })).toBe(
      'Columna de vista',
    );
  });

  it('describes the elements for the checkbox labels', () => {
    expect(accessElement(permissionOf())).toBe('DELETE (Concedido) en Ventas.dbo.Facturas');
    expect(accessElement(roleMembershipOf())).toBe('rol sysadmin');
    expect(
      accessElement(
        permissionOf({
          account: {
            id: 'a1',
            name: 'app',
            host: 'localhost',
            level: 'server',
            database: null,
            type: 'mysql-account',
          },
        }),
      ),
    ).toBe('DELETE (Concedido) en Ventas.dbo.Facturas de app@localhost');
  });

  it('§8.4: explains the parts not read in the last collection', () => {
    expect(unavailableMessage({ section: 'database:Compras', reason: 'offline' })).toBe(
      'Base de datos Compras: No se pudo leer en la última recolección: fuera de línea.',
    );
    expect(unavailableMessage({ section: 'instance', reason: 'timeout' })).toBe(
      'Instancia: No se pudo leer en la última recolección: tiempo agotado.',
    );
  });

  it('describes the change log', () => {
    expect(historyElement(historyEntryOf())).toBe('Cuenta');
    expect(
      historyElement(
        historyEntryOf({
          target: 'permission',
          item: {
            kind: 'permission',
            securable: permissionOf().securable,
            role: null,
            permission: 'SELECT',
            state: 'deny',
          },
        }),
      ),
    ).toBe('SELECT (Denegado) en Ventas.dbo.Facturas');
    expect(changeValue('enabled', 'false')).toBe('No');
    expect(changeValue('type', 'windows-login')).toBe('Login de Windows');
    expect(changeValue('login', null)).toBe('Sin valor');
  });
});

describe('SQL audit state presentation (§11.1)', () => {
  it('RF-14: gives each risk level its MD3 role, icon and text', () => {
    expect(riskStatus('critical')).toEqual({ label: 'Crítico', icon: 'error', tone: 'error' });
    expect(riskStatus('high')).toEqual({ label: 'Alto', icon: 'warning', tone: 'tertiary' });
    expect(riskStatus('general')).toEqual({
      label: 'General',
      icon: 'info',
      tone: 'on-surface-variant',
    });
    expect(riskStatus('denied')).toEqual({
      label: 'Denegado',
      icon: 'block',
      tone: 'on-surface-variant',
    });
  });

  it('RF-17: gives each validation state its MD3 role, icon and text', () => {
    expect(validationStatus('valid')).toEqual({
      label: 'Válido',
      icon: 'check_circle',
      tone: 'primary',
    });
    expect(validationStatus('pending')).toEqual({
      label: 'Pendiente',
      icon: 'pending',
      tone: 'on-surface-variant',
    });
    expect(validationStatus('changed')).toEqual({
      label: 'Cambió desde la validación',
      icon: 'change_circle',
      tone: 'tertiary',
    });
    expect(validationStatus('removed')).toEqual({
      label: 'Eliminado del motor',
      icon: 'remove_circle_outline',
      tone: 'outline',
    });
  });

  it('CA-4: flags only the unverified TLS mode', () => {
    expect(tlsStatus('unverified')).toEqual({
      label: 'Certificado sin verificar',
      icon: 'gpp_maybe',
      tone: 'tertiary',
    });
    expect(tlsStatus('verify')).toBeNull();
    expect(tlsStatus('pinned')).toBeNull();
  });
});
