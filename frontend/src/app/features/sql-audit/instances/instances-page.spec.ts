import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { SqlAuditApi } from '../data/sql-audit-api';
import { iconNames, instanceOf, pageOf, rowTexts } from '../data/sql-audit-fixtures';
import { InstanceQuery, SqlInstanceListItem } from '../data/sql-audit.models';
import { InstancesPage } from './instances-page';

describe('InstancesPage', () => {
  let items: SqlInstanceListItem[];
  let fail: boolean;
  let queries: InstanceQuery[];

  beforeEach(() => {
    items = [];
    fail = false;
    queries = [];
    TestBed.configureTestingModule({
      imports: [InstancesPage],
      providers: [
        provideRouter([]),
        {
          provide: SqlAuditApi,
          useValue: {
            searchInstances: (query: InstanceQuery) => {
              queries.push(query);
              return fail ? throwError(() => new ApiError(500, null)) : of(pageOf(items));
            },
          },
        },
      ],
    });
  });

  const render = async () => {
    const fixture = TestBed.createComponent(InstancesPage);
    await new Promise((resolve) => setTimeout(resolve, 350)); // search debounce
    await vi.waitFor(() => expect(queries.length).toBeGreaterThan(0));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('RF-20: shows device, alias, engine, target, collection, accounts and pending counters', async () => {
    items = [instanceOf({ instanceName: 'PROD', port: null })];

    const element = await render();
    const [row] = rowTexts(element);

    expect(element.querySelector('h1')?.textContent).toBe('Auditoría SQL');
    expect(queries[0].sort).toEqual({ active: 'alias', descending: false });
    expect(row[0]).toBe('ventas');
    expect(row[1]).toBe('SQL Server 16.0.4135');
    expect(row[2]).toBe('srv-db-01\\PROD');
    expect(row[3]).toBe('srv-db-01');
    expect(row[4]).toContain('2026');
    expect(row[5]).toBe('12');
    expect(row[6]).toBe('5');
    expect(row[7]).toBe('1');
    expect(element.querySelector('td a')?.getAttribute('href')).toBe('/sql-audit/instances/i1');
  });

  it('RF-20, CA-4: warns about unreadable parts, unverified TLS and deleted devices', async () => {
    items = [
      instanceOf({
        tls: 'unverified',
        hasUnavailable: true,
        device: { id: 'd1', name: 'srv-db-01', status: 'deleted' },
      }),
      instanceOf({ id: 'i2', alias: 'compras' }),
    ];

    const element = await render();
    const [flagged, clean] = rowTexts(element);
    const firstRow = element.querySelector('tr.mat-mdc-row')!;

    expect(flagged[0]).toBe('ventas Certificado sin verificar Datos no disponibles');
    expect(flagged[3]).toBe('srv-db-01 Equipo eliminado');
    expect(iconNames(firstRow)).toEqual(['gpp_maybe', 'warning', 'delete', 'error']);
    expect(clean[0]).toBe('compras');
  });

  it('§8.4: explains how to start when nothing is audited and links to the manual', async () => {
    const element = await render();
    const link = element.querySelector<HTMLAnchorElement>('td.empty a');

    expect(element.textContent).toContain(
      'Todavía no hay motores SQL auditados. Configúralos en el agente con itmas-agent sql add.',
    );
    expect(element.querySelector('td.empty code')?.textContent).toBe('itmas-agent sql add');
    expect(link?.getAttribute('href')).toBe('/manual/configurar-conexiones-sql');
  });

  it('§8.4: offers to retry when the list cannot be loaded', async () => {
    fail = true;

    const element = await render();
    const alert = element.querySelector('[role="alert"]');

    expect(alert?.textContent).toContain('No se pudo cargar el listado de instancias.');
    expect(iconNames(alert!)).toContain('error');
    expect(alert?.querySelector('button')?.textContent).toContain('Reintentar');
  });

  it('CA-17: renders engine-provided names as text, never as HTML', async () => {
    items = [
      instanceOf({ alias: '<script>alert(1)</script>', host: '<img src=x onerror=alert(1)>' }),
    ];

    const element = await render();

    expect(element.querySelector('td script, td img')).toBeNull();
    expect(element.textContent).toContain('<script>alert(1)</script>');
    expect(element.textContent).toContain('<img src=x onerror=alert(1)>');
  });
});
