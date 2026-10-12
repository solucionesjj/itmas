import { TestBed } from '@angular/core/testing';
import { MatTooltip } from '@angular/material/tooltip';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { Observable, Subject, of } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { NotificationService } from '../../../core/services/notification-service';
import { NO_PERMISSION_HINT, REMOVED_HINT } from '../components/validation-checkbox';
import { SqlAuditApi } from '../data/sql-audit-api';
import {
  accountDetailOf,
  accountOf,
  instanceDetailOf,
  iconNames,
  pageOf,
  rowTexts,
} from '../data/sql-audit-fixtures';
import { AccountQuery, SqlAccountListItem, Validation } from '../data/sql-audit.models';
import {
  ACCOUNT_CONFLICT_MESSAGE,
  PendingChange,
  SAVE_ERROR_MESSAGE,
} from '../data/sql-validation';
import { InstanceAccountsTab } from './instance-accounts-tab';

describe('InstanceAccountsTab', () => {
  let items: SqlAccountListItem[];
  let permissions: string[];
  let queries: AccountQuery[];
  let saving: Subject<Validation>;
  const notifications = { error: vi.fn(), info: vi.fn() };
  const api = {
    searchAccounts: vi.fn(),
    validateAccount: vi.fn(),
    unvalidateAccount: vi.fn(),
    getAccount: vi.fn(),
  };

  beforeEach(() => {
    items = [];
    queries = [];
    permissions = ['sql-audit.read', 'sql-audit.accesses.validate'];
    saving = new Subject<Validation>();
    notifications.error.mockReset();
    api.searchAccounts.mockReset().mockImplementation((_id: string, query: AccountQuery) => {
      queries.push(query);
      return of(pageOf(items));
    });
    api.validateAccount.mockReset().mockReturnValue(saving);
    api.unvalidateAccount.mockReset().mockReturnValue(saving);
    api.getAccount.mockReset();
    TestBed.configureTestingModule({
      imports: [InstanceAccountsTab],
      providers: [
        provideRouter([]),
        { provide: SqlAuditApi, useValue: api },
        { provide: NotificationService, useValue: notifications },
        {
          provide: AuthService,
          useValue: { hasPermission: (p: string) => permissions.includes(p) },
        },
      ],
    });
  });

  const render = async () => {
    const fixture = TestBed.createComponent(InstanceAccountsTab);
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

  const cells = rowTexts;
  const icons = (element: HTMLElement) => iconNames(element.querySelector('tr.mat-mdc-row')!);

  const checkbox = (element: HTMLElement) =>
    element.querySelector<HTMLInputElement>('input[type="checkbox"]')!;

  it('P-15: shows the accounts with pending elements by default', async () => {
    const { element } = await render();

    expect(queries[0].validation).toBe('pending');
    expect(element.textContent).toContain('No hay elementos pendientes de validar.');
  });

  it('RF-21: shows level, name, type, risk (inherited), roles, pending and state', async () => {
    items = [accountOf()];

    const { element } = await render();
    const [row] = cells(element);

    expect(row[0]).toBe('Motor');
    expect(row[1]).toBe('app_ventas');
    expect(row[2]).toBe('Login SQL');
    expect(row[3]).toBe('Sí');
    expect(row[4]).toBe('Crítico (heredado)');
    expect(row[5]).toBe('sysadmin');
    expect(row[6]).toBe('3');
    expect(row[7]).toBe('Pendiente');
    expect(icons(element)).toEqual(['error', 'pending']);
  });

  it('labels the checkbox with the element and keeps it usable with the keyboard', async () => {
    items = [accountOf({ name: 'app', host: '%' })];

    const input = checkbox((await render()).element);

    expect(input.getAttribute('aria-label')).toBe('Marcar como válido: app@%');
    expect(input.tabIndex).toBe(0);
    expect(input.disabled).toBe(false);
  });

  it('RF-16: marks at once, updates the counters and keeps the API result', async () => {
    items = [accountOf()];
    const { fixture, element, changes } = await render();

    checkbox(element).click();
    await settle(fixture);

    expect(api.validateAccount).toHaveBeenCalledWith('a1', 'f'.repeat(64));
    expect(checkbox(element).checked).toBe(true);
    expect(cells(element)[0][6]).toBe('2');
    expect(cells(element)[0][7]).toBe('Válido');
    expect(changes).toEqual([{ delta: -1, critical: false }]);

    saving.next({ status: 'valid', by: 'Ana', at: '2026-10-11T10:00:00Z' });
    saving.complete();
    await settle(fixture);

    expect(checkbox(element).checked).toBe(true);
    expect(cells(element)[0][6]).toBe('2');
    expect(notifications.error).not.toHaveBeenCalled();
  });

  it('§8.4: reverts the checkbox and the counters when saving fails', async () => {
    items = [accountOf({ validation: { status: 'valid', by: 'Ana', at: null }, pendingCount: 0 })];
    const { fixture, element, changes } = await render();

    checkbox(element).click();
    await settle(fixture);
    expect(api.unvalidateAccount).toHaveBeenCalledWith('a1');
    expect(cells(element)[0][6]).toBe('1');

    saving.error(new ApiError(0, null));
    await settle(fixture);

    expect(checkbox(element).checked).toBe(true);
    expect(cells(element)[0][6]).toBe('0');
    expect(changes).toEqual([
      { delta: 1, critical: false },
      { delta: -1, critical: false },
    ]);
    expect(notifications.error).toHaveBeenCalledWith(SAVE_ERROR_MESSAGE);
  });

  it('CA-12: on 409 explains the change and refreshes the account row', async () => {
    items = [accountOf()];
    const fresh = accountDetailOf({
      validation: { status: 'changed', by: 'Ana', at: null },
      fingerprint: 'e'.repeat(64),
      enabled: false,
    });
    api.getAccount.mockReturnValue(of(fresh) as Observable<unknown>);
    const { fixture, element } = await render();

    checkbox(element).click();
    await settle(fixture);
    saving.error(new ApiError(409, { status: 409, title: 'Conflict' }));
    await settle(fixture);

    expect(notifications.error).toHaveBeenCalledWith(ACCOUNT_CONFLICT_MESSAGE);
    expect(api.getAccount).toHaveBeenCalledWith('a1');
    expect(checkbox(element).checked).toBe(false);
    expect(cells(element)[0][3]).toBe('No');
    expect(cells(element)[0][7]).toBe('Cambió desde la validación');
  });

  it('disables the checkbox with an explanation without the validation permission', async () => {
    permissions = ['sql-audit.read'];
    items = [accountOf()];
    const { fixture, element } = await render();

    const input = checkbox(element);
    expect(input.getAttribute('aria-disabled')).toBe('true');
    const tooltip = fixture.debugElement.query(By.directive(MatTooltip)).injector.get(MatTooltip);
    expect(tooltip.message).toBe(NO_PERMISSION_HINT);
    expect(tooltip.disabled).toBe(false);
    input.click();
    await settle(fixture);

    expect(api.validateAccount).not.toHaveBeenCalled();
    expect(input.checked).toBe(false);
  });

  it('disables the checkbox of accounts removed from the engine', async () => {
    items = [
      accountOf({
        removedAt: '2026-10-10T06:00:00Z',
        validation: { status: 'removed', by: null, at: null },
      }),
    ];

    const { fixture, element } = await render();

    expect(checkbox(element).getAttribute('aria-disabled')).toBe('true');
    expect(
      fixture.debugElement.query(By.directive(MatTooltip)).injector.get(MatTooltip).message,
    ).toBe(REMOVED_HINT);
    expect(cells(element)[0][7]).toBe('Eliminado del motor');
  });

  it('CA-17: renders engine names as text, never as HTML', async () => {
    items = [accountOf({ name: '<script>alert(1)</script>', roles: ['<b>rol</b>'] })];

    const { element } = await render();

    expect(element.querySelector('td script, td b')).toBeNull();
    expect(element.textContent).toContain('<script>alert(1)</script>');
    expect(element.textContent).toContain('<b>rol</b>');
    expect(checkbox(element).getAttribute('aria-label')).toBe(
      'Marcar como válido: <script>alert(1)</script>',
    );
  });
});
