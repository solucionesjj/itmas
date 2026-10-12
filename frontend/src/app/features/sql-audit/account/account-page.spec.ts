import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Subject, of } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { NotificationService } from '../../../core/services/notification-service';
import { SqlAuditApi } from '../data/sql-audit-api';
import {
  accountDetailOf,
  historyEntryOf,
  pageOf,
  permissionOf,
  roleMembershipOf,
  rowTexts,
  readable,
} from '../data/sql-audit-fixtures';
import { AccountItemQuery, SqlAccountDetail, Validation } from '../data/sql-audit.models';
import { ACCOUNT_CONFLICT_MESSAGE } from '../data/sql-validation';
import { AccountPage } from './account-page';

describe('AccountPage', () => {
  let account: SqlAccountDetail;
  let saving: Subject<Validation>;
  let itemQueries: AccountItemQuery[];
  const notifications = { error: vi.fn(), info: vi.fn() };
  const api = {
    getAccount: vi.fn(),
    getAccountItems: vi.fn(),
    getAccountChanges: vi.fn(),
    validateAccount: vi.fn(),
    unvalidateAccount: vi.fn(),
    validateItem: vi.fn(),
    unvalidateItem: vi.fn(),
  };

  beforeEach(() => {
    account = accountDetailOf();
    saving = new Subject<Validation>();
    itemQueries = [];
    notifications.error.mockReset();
    api.getAccount.mockReset().mockImplementation(() => of(account));
    api.getAccountItems.mockReset().mockImplementation((_id: string, query: AccountItemQuery) => {
      itemQueries.push(query);
      return of(pageOf(query.kind === 'permission' ? [permissionOf()] : [roleMembershipOf()]));
    });
    api.getAccountChanges.mockReset().mockReturnValue(of(pageOf([historyEntryOf()])));
    for (const method of [
      api.validateAccount,
      api.unvalidateAccount,
      api.validateItem,
      api.unvalidateItem,
    ]) {
      method.mockReset().mockReturnValue(saving);
    }
    TestBed.configureTestingModule({
      imports: [AccountPage],
      providers: [
        provideRouter([]),
        { provide: SqlAuditApi, useValue: api },
        { provide: NotificationService, useValue: notifications },
        { provide: AuthService, useValue: { hasPermission: () => true } },
      ],
    });
  });

  const render = async () => {
    const fixture = TestBed.createComponent(AccountPage);
    fixture.componentRef.setInput('id', 'a1');
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return { fixture, element: fixture.nativeElement as HTMLElement };
  };

  const settle = async (fixture: { detectChanges(): void; whenStable(): Promise<unknown> }) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const text = (element: Element) => readable(element, true);
  const accountCheckbox = (element: HTMLElement) =>
    element.querySelector<HTMLInputElement>('.page-header input[type="checkbox"]')!;
  const tables = (element: HTMLElement) => [...element.querySelectorAll('table')];

  it('RF-22: shows the attributes, the inherited risk, roles, permissions and change log', async () => {
    const { element } = await render();

    expect(element.querySelector('h1')?.textContent).toBe('app_ventas');
    expect(text(element.querySelector('.breadcrumb')!)).toBe('Auditoría SQL / ventas / app_ventas');
    expect(text(element.querySelector('.summary')!)).toBe(
      'error Crítico (heredado) pending Pendiente 3 elementos pendientes',
    );
    expect(text(element.querySelector('.facts')!)).toContain('Riesgo propio info General');
    expect(text(element.querySelector('.facts')!)).toContain(
      'Riesgo heredado de sus roles error Crítico',
    );
    expect(itemQueries.map((query) => query.kind)).toEqual(['role-membership', 'permission']);
    expect(itemQueries[1].validation).toBe('');

    const [roles, permissions, history] = tables(element).map((table) => rowTexts(table));
    expect(roles[0].slice(0, 3)).toEqual(['sysadmin', 'Crítico', 'Pendiente']);
    expect(permissions[0].slice(0, 6)).toEqual([
      'Ventas.dbo.Facturas Tabla',
      'DELETE',
      'Concedido',
      'dbo',
      'Alto',
      'Pendiente',
    ]);
    expect(history[0].slice(1)).toEqual([
      'Cambió',
      'Cuenta Habilitada: Sí → No',
      'Detectado en la recolección',
    ]);
  });

  it('RN-6: validates the account with the fingerprint it shows and updates the counter', async () => {
    const { fixture, element } = await render();

    accountCheckbox(element).click();
    await settle(fixture);

    expect(api.validateAccount).toHaveBeenCalledWith('a1', 'f'.repeat(64));
    expect(text(element.querySelector('.summary')!)).toContain('2 elementos pendientes');

    saving.next({ status: 'valid', by: 'Ana', at: '2026-10-11T10:00:00Z' });
    saving.complete();
    await settle(fixture);

    expect(accountCheckbox(element).checked).toBe(true);
    expect(api.getAccountChanges).toHaveBeenCalledTimes(2);
  });

  it('CA-12: on 409 explains the change and reloads the account with its new fingerprint', async () => {
    const { fixture, element } = await render();
    account = accountDetailOf({
      validation: { status: 'changed', by: 'Ana', at: null },
      fingerprint: 'e'.repeat(64),
    });

    accountCheckbox(element).click();
    await settle(fixture);
    saving.error(new ApiError(409, { status: 409 }));
    await settle(fixture);

    expect(notifications.error).toHaveBeenCalledWith(ACCOUNT_CONFLICT_MESSAGE);
    expect(api.getAccount).toHaveBeenCalledTimes(2);
    expect(accountCheckbox(element).checked).toBe(false);
    expect(text(element)).toContain('Esta cuenta cambió desde su validación.');
  });

  it('RF-18: validating a permission updates the pending elements of the account at once', async () => {
    const { fixture, element } = await render();
    const permissionCheckbox =
      tables(element)[1].querySelector<HTMLInputElement>('input[type="checkbox"]')!;

    permissionCheckbox.click();
    await settle(fixture);

    expect(api.validateItem).toHaveBeenCalledWith('p1');
    expect(text(element.querySelector('.summary')!)).toContain('2 elementos pendientes');
  });

  it('CA-17: renders the account name as text, never as HTML', async () => {
    account = accountDetailOf({ name: '<script>alert(1)</script>', host: null });

    const { element } = await render();

    expect(element.querySelector('h1 script, .breadcrumb script')).toBeNull();
    expect(element.querySelector('h1')?.textContent).toBe('<script>alert(1)</script>');
    expect(accountCheckbox(element).getAttribute('aria-label')).toBe(
      'Marcar como válido: <script>alert(1)</script>',
    );
  });
});
