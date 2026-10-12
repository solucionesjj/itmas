import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { SqlAuditApi } from '../data/sql-audit-api';
import { iconNames, instanceDetailOf, pageOf, readable } from '../data/sql-audit-fixtures';
import { SqlInstanceDetail } from '../data/sql-audit.models';
import { InstancePage } from './instance-page';

describe('InstancePage', () => {
  let response: () => Observable<SqlInstanceDetail>;

  beforeEach(() => {
    response = () => of(instanceDetailOf());
    TestBed.configureTestingModule({
      imports: [InstancePage],
      providers: [
        provideRouter([]),
        {
          provide: SqlAuditApi,
          useValue: {
            getInstance: () => response(),
            searchAccounts: () => of(pageOf([])),
            searchPermissions: () => of(pageOf([])),
          },
        },
        { provide: AuthService, useValue: { hasPermission: () => true } },
      ],
    });
  });

  const render = async (tab?: string) => {
    const fixture = TestBed.createComponent(InstancePage);
    fixture.componentRef.setInput('id', 'i1');
    if (tab) {
      fixture.componentRef.setInput('tab', tab);
    }
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  const text = (element: Element) => readable(element, true);
  const notices = (element: HTMLElement) => [...element.querySelectorAll('.notice')];

  it('shows the instance with the tabs "Cuentas" and "Permisos"', async () => {
    const element = await render();

    expect(element.querySelector('h1')?.textContent).toContain('ventas');
    expect(text(element.querySelector('.breadcrumb')!)).toBe('Auditoría SQL / ventas');
    expect([...element.querySelectorAll('[role="tab"]')].map((tab) => text(tab))).toEqual([
      'Cuentas',
      'Permisos',
    ]);
    expect(text(element)).toContain('Mixto (SQL y Windows)');
    expect(notices(element)).toEqual([]);
  });

  it('opens the tab kept in the URL', async () => {
    const element = await render('permissions');

    expect(element.querySelector('[role="tab"][aria-selected="true"]')?.textContent).toContain(
      'Permisos',
    );
    expect(element.querySelector('app-instance-permissions-tab')).not.toBeNull();
  });

  it('RF-13, CA-4, RN-8: warns about unverified TLS, unreadable parts and a deleted device', async () => {
    response = () =>
      of(
        instanceDetailOf({
          tls: 'unverified',
          hasUnavailable: true,
          unavailable: [{ section: 'database:Compras', reason: 'permission-denied' }],
          device: { id: 'd1', name: 'srv-db-01', status: 'deleted' },
        }),
      );

    const [tls, unavailable, deleted] = notices(await render());

    expect(text(tls)).toContain('Certificado sin verificar');
    expect(iconNames(tls)).toEqual(['gpp_maybe']);
    expect(text(unavailable)).toBe(
      'warning Base de datos Compras: No se pudo leer en la última recolección: permiso denegado.',
    );
    expect(text(deleted)).toContain('fue eliminado');
  });

  it('tells apart a missing instance from a load error', async () => {
    response = () => throwError(() => new ApiError(404, null));
    expect(text(await render())).toContain('La instancia no existe o ya no se conserva.');

    response = () => throwError(() => new ApiError(503, null));
    const element = await render();
    expect(element.querySelector('[role="alert"]')?.textContent).toContain('Reintentar');
  });
});
