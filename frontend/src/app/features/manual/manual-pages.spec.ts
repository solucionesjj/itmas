import { ComponentRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Route, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { routes } from '../../app.routes';
import { ManualApi } from './data/manual-api';
import { ManualIndex } from './data/manual.models';
import { ManualIndexPage } from './manual-index-page';
import { ManualProcessPage } from './manual-process-page';

const INDEX: ManualIndex = {
  categories: [
    { id: 'inventario', title: 'Inventario de equipos' },
    { id: 'usuarios', title: 'Usuarios y privilegios' },
  ],
  processes: [
    {
      id: 'instalar-agente',
      title: 'Instalar el agente y enrolar el equipo',
      summary: 'Instalar el agente con el código del equipo.',
      category: 'inventario',
      permissions: [],
      specs: ['SPEC-0003'],
      updated: '2026-10-10',
      routes: [],
    },
    {
      id: 'asignar-politicas',
      title: 'Asignar políticas',
      summary: 'Conceder privilegios.',
      category: 'usuarios',
      permissions: ['users.policies.assign'],
      specs: ['SPEC-0001'],
      updated: '2026-10-10',
      routes: [],
    },
  ],
};

describe('User manual pages', () => {
  let content: () => Observable<string>;

  beforeEach(() => {
    content = () =>
      of('# Asignar políticas\n\nTexto con **negrita** y [un enlace](instalar-agente).');
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: ManualApi,
          useValue: {
            getIndex: () => of(INDEX),
            getContent: () => content(),
            reset: () => undefined,
          },
        },
      ],
    });
  });

  const text = (element: HTMLElement) => element.textContent?.replace(/\s+/g, ' ') ?? '';

  const renderIndex = async (query = '') => {
    const fixture = TestBed.createComponent(ManualIndexPage);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    if (query) {
      const input = element.querySelector('input')!;
      input.value = query;
      input.dispatchEvent(new Event('input'));
    }
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return element;
  };

  const renderProcess = async (id: string) => {
    const fixture = TestBed.createComponent(ManualProcessPage);
    (fixture.componentRef as ComponentRef<ManualProcessPage>).setInput('id', id);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it('CA-1: is public: the manual routes are outside the authenticated shell and have no guard', () => {
    const manual = routes.find((route) => route.path === 'manual') as Route;
    expect(manual).toBeDefined();
    expect(manual.canActivate ?? []).toEqual([]);
    expect(manual.canActivateChild ?? []).toEqual([]);
  });

  it('CA-1: lists every process grouped by category, with its permissions', async () => {
    const content = text(await renderIndex());

    expect(content).toContain('Inventario de equipos');
    expect(content).toContain('Usuarios y privilegios');
    expect(content).toContain('Cualquier usuario');
    expect(content).toContain('users.policies.assign');
  });

  it('CA-2: searches ignoring case and accents', async () => {
    const element = await renderIndex('ENROLÁR');
    const content = text(element);

    expect(content).toContain('Instalar el agente y enrolar el equipo');
    expect(content).not.toContain('Asignar políticas');
    expect(text(await renderIndex('xyz'))).toContain('Ningún proceso coincide con la búsqueda.');
  });

  it('CA-3: shows a process with its metadata and links to other processes inside the manual', async () => {
    const element = await renderProcess('asignar-politicas');

    expect(element.querySelector('h1')?.textContent).toContain('Asignar políticas');
    expect(element.querySelectorAll('h1').length).toBe(1);
    expect(text(element)).toContain('SPEC-0001');
    expect(element.querySelector('.content strong')?.textContent).toBe('negrita');
    expect(element.querySelector('.content a')?.getAttribute('href')).toBe(
      '/manual/instalar-agente',
    );
  });

  it('explains unknown processes without requesting any file', async () => {
    let requested = false;
    content = () => {
      requested = true;
      return of('');
    };

    const element = await renderProcess('../../index');

    expect(text(element)).toContain('Este proceso no existe o se ha retirado del manual.');
    expect(requested).toBe(false);
  });

  it('CA-6: sanitizes the HTML produced from the Markdown', async () => {
    content = () =>
      of(
        '# Asignar políticas\n\n<script>window.hacked = true</script>\n\n<img src="x" onerror="window.hacked = true">',
      );

    const element = await renderProcess('asignar-politicas');

    expect(element.querySelector('.content script')).toBeNull();
    expect(element.querySelector('.content img')?.getAttribute('onerror')).toBeNull();
    expect((window as unknown as { hacked?: boolean }).hacked).toBeUndefined();
  });

  it('offers a retry when the manual cannot be loaded', async () => {
    TestBed.overrideProvider(ManualApi, {
      useValue: { getIndex: () => throwError(() => new Error('offline')), reset: () => undefined },
    });

    expect(text(await renderIndex())).toContain('No se pudo cargar el manual.');
  });
});
