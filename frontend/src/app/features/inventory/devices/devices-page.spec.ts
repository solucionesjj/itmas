import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { InventoryApi } from '../data/inventory-api';
import { deviceOf } from '../data/inventory-fixtures';
import { DeviceListItem } from '../data/inventory.models';
import { DevicesPage } from './devices-page';

describe('DevicesPage', () => {
  let items: DeviceListItem[];
  let permissions: string[];

  beforeEach(() => {
    items = [];
    permissions = ['devices.read', 'devices.reports.read'];
    TestBed.configureTestingModule({
      imports: [DevicesPage],
      providers: [
        provideRouter([]),
        {
          provide: InventoryApi,
          useValue: {
            searchDevices: () => of({ items, page: 1, pageSize: 20, total: items.length }),
          },
        },
        {
          provide: AuthService,
          useValue: { hasPermission: (p: string) => permissions.includes(p) },
        },
      ],
    });
  });

  const render = async () => {
    const fixture = TestBed.createComponent(DevicesPage);
    await new Promise((resolve) => setTimeout(resolve, 350)); // search debounce
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  const rows = (element: HTMLElement) =>
    [...element.querySelectorAll('tr.mat-mdc-row')].map((row) =>
      [...row.querySelectorAll('td')].map((cell) => cell.textContent?.replace(/\s+/g, ' ').trim()),
    );

  it('CA-9: shows each device summary with the definition wording for missing data', async () => {
    items = [
      deviceOf({ name: 'pc-ana', hostname: 'PC-ANA-01' }),
      deviceOf({
        id: 'd2',
        name: 'srv-web',
        type: 'server',
        hostname: 'srv-web',
        owner: { email: 'ana@example.com', name: 'Ana Pérez' },
        lastCollectedAt: '2026-10-09T12:00:00Z',
        reportCount: 7,
      }),
    ];

    const [first, second] = rows(await render());

    expect(first[0]).toBe('pc-ana PC-ANA-01');
    expect(first[1]).toBe('Estación de trabajo');
    expect(first[2]).toBe('Sin usuario asociado');
    expect(first[3]).toBe('Sin registros');
    expect(first[4]).toBe('0');
    expect(second[0]).toBe('srv-web');
    expect(second[1]).toBe('Servidor');
    expect(second[2]).toBe('Ana Pérez ana@example.com');
    expect(second[3]).toContain('2026');
    expect(second[4]).toBe('7');
  });

  it('RF-14: disables the latest report of devices without records', async () => {
    items = [deviceOf({ reportCount: 0 }), deviceOf({ id: 'd2', name: 'b', reportCount: 3 })];

    const buttons = [
      ...(await render()).querySelectorAll<HTMLButtonElement>(
        'button[aria-label^="Ver último informe"]',
      ),
    ];

    expect(buttons.map((button) => button.disabled)).toEqual([true, false]);
  });

  it('hides the collected data and management actions without their permissions', async () => {
    permissions = ['devices.read'];
    items = [deviceOf({ reportCount: 3 })];

    const element = await render();

    expect(element.querySelector('button[aria-label^="Ver registros"]')).toBeNull();
    expect(element.querySelector('button[aria-label^="Más acciones"]')).toBeNull();
    expect(element.textContent).not.toContain('Nuevo equipo');
  });

  it('CA-13: renders device data as text, never as HTML', async () => {
    items = [deviceOf({ name: '<img src=x onerror=alert(1)>', hostname: '<b>host</b>' })];

    const element = await render();

    expect(element.querySelector('td img, td b')).toBeNull();
    expect(element.textContent).toContain('<img src=x onerror=alert(1)>');
  });

  it('distinguishes an empty inventory from a search without results', async () => {
    expect((await render()).textContent).toContain('Todavía no hay equipos registrados.');
  });
});
