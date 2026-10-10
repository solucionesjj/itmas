import { ComponentRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { InventoryApi } from '../data/inventory-api';
import { INVENTORY, reportOf } from '../data/inventory-fixtures';
import { DeviceReport } from '../data/inventory.models';
import { DeviceReportPage } from './device-report-page';

describe('DeviceReportPage', () => {
  let response: () => Observable<DeviceReport>;
  let requested: (string | undefined)[];

  beforeEach(() => {
    requested = [];
    response = () => of(reportOf());
    TestBed.configureTestingModule({
      imports: [DeviceReportPage],
      providers: [
        provideRouter([]),
        {
          provide: InventoryApi,
          useValue: {
            getReport: (_id: string, reportId?: string) => {
              requested.push(reportId);
              return response();
            },
          },
        },
      ],
    });
  });

  const render = async (reportId?: string) => {
    const fixture = TestBed.createComponent(DeviceReportPage);
    const ref = fixture.componentRef as ComponentRef<DeviceReportPage>;
    ref.setInput('id', 'd1');
    if (reportId) {
      ref.setInput('reportId', reportId);
    }
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  const text = (element: HTMLElement) => element.textContent?.replace(/\s+/g, ' ') ?? '';

  it('CA-12: shows the latest record grouped by sections', async () => {
    const element = await render();
    const content = text(element);

    expect(requested).toEqual([undefined]);
    for (const title of [
      'Características del equipo',
      'Seguridad',
      'Red',
      'Almacenamiento',
      'Usuarios locales',
      'Software instalado',
    ]) {
      expect(content).toContain(title);
    }
    expect(content).toContain('Servidor web');
    expect(content).toContain('Sin usuario asociado');
    expect(content).toContain('Ubuntu (Linux)');
    expect(content).toContain('16 GB');
    expect(content).toContain('Intel Xeon E-2236 · 6 núcleos físicos · 12 procesadores lógicos');
    expect(content).toContain('Cifrado (LUKS)');
    expect(content).toContain('25 B libres (25 %)');
    expect(content).toContain('Bogotá, Bogotá D.C., Colombia');
    expect(content).toContain('3 aplicaciones');
    expect(content).not.toContain('Datos no disponibles');
    expect(content).not.toContain('Este no es el último registro');
  });

  it('lists the software alphabetically', async () => {
    const element = await render();

    const names = [...element.querySelectorAll('.software-list li')].map((item) =>
      item.textContent?.trim(),
    );
    expect(names).toEqual(['Git', 'nginx', 'zsh']);
  });

  it('RN-6: marks unavailable sections with their reason instead of inventing values', async () => {
    response = () =>
      of(
        reportOf(
          {},
          {
            hardware: { ...INVENTORY.hardware, memoryBytes: null },
            network: { ...INVENTORY.network, publicNetwork: null },
            unavailable: [
              { section: 'hardware.memory', reason: 'permission-denied' },
              { section: 'network.publicNetwork', reason: 'timeout' },
            ],
          },
        ),
      );

    const element = await render();
    const content = text(element);

    expect(content).toContain('No disponible (permiso denegado)');
    expect(content).toContain('No disponible (tiempo agotado)');
    expect(content).toContain('Datos no disponibles');
    const reasons = [
      ...element.querySelectorAll('#section-unavailable dt, #section-unavailable dd'),
    ].map((item) => item.textContent?.trim());
    expect(reasons).toEqual(['Memoria RAM', 'Permiso denegado', 'IP pública', 'Tiempo agotado']);
  });

  it('warns when the declared public IP and the connection IP differ', async () => {
    response = () => of(reportOf({ connectionIp: '198.51.100.9' }));

    expect(text(await render())).toContain('no coinciden');
  });

  it('opens a record of the history and points to the latest one', async () => {
    response = () => of(reportOf({ isLatest: false }));

    const content = text(await render('r0'));

    expect(requested).toEqual(['r0']);
    expect(content).toContain('Este no es el último registro del equipo.');
  });

  it('CA-13: renders device data as text, never as HTML', async () => {
    response = () =>
      of(
        reportOf(
          {},
          {
            system: { ...INVENTORY.system, hostname: '<script>alert(1)</script>' },
            software: { applications: ['<b>x</b>'] },
          },
        ),
      );

    const element = await render();

    expect(element.querySelector('script, .software-list b')).toBeNull();
    expect(text(element)).toContain('<script>alert(1)</script>');
  });

  it('explains when the device has no records yet', async () => {
    response = () => throwError(() => new ApiError(404, null));

    expect(text(await render())).toContain('Este equipo todavía no ha enviado inventario.');
  });
});
