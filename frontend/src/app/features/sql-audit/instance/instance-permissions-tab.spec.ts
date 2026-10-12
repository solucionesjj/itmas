import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { NotificationService } from '../../../core/services/notification-service';
import { SqlAuditApi } from '../data/sql-audit-api';
import {
  iconNames,
  instanceDetailOf,
  pageOf,
  permissionOf,
  rowTexts,
} from '../data/sql-audit-fixtures';
import { PermissionQuery, SqlAccessItem, Validation } from '../data/sql-audit.models';
import { PendingChange } from '../data/sql-validation';
import { InstancePermissionsTab } from './instance-permissions-tab';

describe('InstancePermissionsTab', () => {
  let items: SqlAccessItem[];
  let queries: PermissionQuery[];
  let saving: Subject<Validation>;
  const notifications = { error: vi.fn(), info: vi.fn() };
  const api = { searchPermissions: vi.fn(), validateItem: vi.fn(), unvalidateItem: vi.fn() };

  const account = {
    id: 'a1',
    name: 'app_ventas',
    level: 'database' as const,
    database: 'Ventas',
    host: null,
    type: 'database-user',
  };

  beforeEach(() => {
    items = [];
    queries = [];
    saving = new Subject<Validation>();
    notifications.error.mockReset();
    api.searchPermissions.mockReset().mockImplementation((_id: string, query: PermissionQuery) => {
      queries.push(query);
      return of(pageOf(items));
    });
    api.validateItem.mockReset().mockReturnValue(saving);
    api.unvalidateItem.mockReset().mockReturnValue(saving);
    TestBed.configureTestingModule({
      imports: [InstancePermissionsTab],
      providers: [
        provideRouter([]),
        { provide: SqlAuditApi, useValue: api },
        { provide: NotificationService, useValue: notifications },
        { provide: AuthService, useValue: { hasPermission: () => true } },
      ],
    });
  });

  const render = async () => {
    const fixture = TestBed.createComponent(InstancePermissionsTab);
    fixture.componentRef.setInput('instance', instanceDetailOf());
    const changes: PendingChange[] = [];
    fixture.componentInstance.pendingChange.subscribe((change) => changes.push(change));
    await new Promise((resolve) => setTimeout(resolve, 350)); // search debounce
    await vi.waitFor(() => expect(queries.length).toBeGreaterThan(0));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement, changes };
  };

  const settle = async (fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  it('starts with the pending permissions, most risky first', async () => {
    const { element } = await render();

    expect(queries[0]).toMatchObject({
      validation: 'pending',
      sort: { active: 'risk', descending: true },
      page: 1,
    });
    expect(element.textContent).toContain('No hay elementos pendientes de validar.');
  });

  it('RF-23: shows account, securable path, permission, state, grantor, risk and state', async () => {
    items = [
      permissionOf({ account }),
      permissionOf({ id: 'p2', account, state: 'deny', risk: 'denied', permission: 'SELECT' }),
    ];

    const { element } = await render();
    const [granted, denied] = rowTexts(element);

    expect(granted.slice(0, 8)).toEqual([
      'app_ventas Ventas',
      'Ventas.dbo.Facturas Tabla',
      'DELETE',
      'Concedido',
      'dbo',
      'Alto',
      'Pendiente',
      expect.stringContaining('2026'),
    ]);
    expect(denied[3]).toBe('Denegado');
    expect(denied[5]).toBe('Denegado');
    expect(iconNames(element.querySelectorAll('tr.mat-mdc-row')[1])).toEqual(['block', 'pending']);
    expect(element.querySelector('input[type="checkbox"]')?.getAttribute('aria-label')).toBe(
      'Marcar como válido: DELETE (Concedido) en Ventas.dbo.Facturas de app_ventas',
    );
  });

  it('updates the critical counter of the instance when a critical permission is validated', async () => {
    items = [permissionOf({ account, risk: 'critical' })];
    const { fixture, element, changes } = await render();

    element.querySelector<HTMLInputElement>('input[type="checkbox"]')!.click();
    await settle(fixture);

    expect(api.validateItem).toHaveBeenCalledWith('p1');
    expect(rowTexts(element)[0][6]).toBe('Válido');
    expect(changes).toEqual([{ delta: -1, critical: true }]);
  });

  it('CA-12: reloads the table when the permission was removed meanwhile (422)', async () => {
    items = [permissionOf({ account })];
    const { fixture, element } = await render();

    element.querySelector<HTMLInputElement>('input[type="checkbox"]')!.click();
    await settle(fixture);
    saving.error(
      new ApiError(422, { status: 422, detail: 'El permiso ya no existe en el motor.' }),
    );
    await settle(fixture);

    expect(notifications.error).toHaveBeenCalledWith('El permiso ya no existe en el motor.');
    expect(queries.length).toBe(2);
  });

  it('CA-17: renders object and account names as text, never as HTML', async () => {
    items = [
      permissionOf({
        account: { ...account, name: '<script>alert(1)</script>' },
        securable: {
          class: 'object',
          database: '<b>db</b>',
          schema: 'dbo',
          name: '<img src=x onerror=alert(1)>',
          objectType: 'table',
          column: null,
        },
        grantor: '<i>x</i>',
      }),
    ];

    const { element } = await render();

    expect(element.querySelector('td script, td b, td img, td i')).toBeNull();
    expect(element.textContent).toContain('<script>alert(1)</script>');
    expect(element.textContent).toContain('<b>db</b>.dbo.<img src=x onerror=alert(1)>');
  });
});
