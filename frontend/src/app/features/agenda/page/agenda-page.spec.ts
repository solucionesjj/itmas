import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { Subject, of, throwError } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { AgendaCalendar } from '../calendar/agenda-calendar';
import { AgendaApi } from '../data/agenda-api';
import { TYPES, summaryOf, summaryTotals } from '../data/agenda-fixtures';
import { EventList, EventSummary } from '../data/agenda.models';
import { AgendaActions } from '../events/agenda-actions';
import { AgendaPage } from './agenda-page';

@Component({ template: '' })
class BlankPage {}

const ALL_PERMISSIONS = [
  'agenda.read',
  'agenda.create',
  'agenda.update',
  'agenda.publish',
  'agenda.complete',
  'agenda.types.manage',
];

describe('AgendaPage (RF-12, RF-13, §8.4)', () => {
  beforeAll(() => {
    // jsdom has no ResizeObserver, which FullCalendar (the calendar views) uses to measure itself.
    (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver ??= class {
      observe(): void {}
      unobserve(): void {}
      disconnect(): void {}
    };
  });

  let items: EventSummary[];
  let truncated: boolean;
  let outlookEnabled: boolean;
  let fail: boolean;
  let permissions: string[];
  const api = { list: vi.fn(), summary: vi.fn(), listTypes: vi.fn(), listTags: vi.fn() };
  const actions = {
    schedule: vi.fn(),
    cancel: vi.fn(),
    complete: vi.fn(),
    deleteDraft: vi.fn(),
    sync: vi.fn(),
    syncAll: vi.fn(),
    exportCalendar: vi.fn(),
  };

  beforeEach(() => {
    // The page works with "today" (the default year); fix it so the expectations do not depend on when the suite runs.
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-10-18T12:00:00Z'));

    items = [];
    truncated = false;
    outlookEnabled = false;
    fail = false;
    permissions = ALL_PERMISSIONS;
    api.list.mockReset().mockImplementation(() =>
      fail
        ? throwError(() => new ApiError(500, null))
        : of({
            items,
            truncated,
            timeZone: 'America/Bogota',
            outlookEnabled,
          } satisfies EventList),
    );
    api.summary.mockReset().mockImplementation(() => of(summaryTotals()));
    api.listTypes.mockReset().mockReturnValue(of(TYPES));
    api.listTags.mockReset().mockReturnValue(of([]));
    for (const action of Object.values(actions)) {
      action.mockReset().mockReturnValue(of(true));
    }
    TestBed.configureTestingModule({
      providers: [
        provideRouter([
          { path: 'agenda', component: AgendaPage },
          { path: 'agenda/events/:id', component: BlankPage },
        ]),
        { provide: AgendaApi, useValue: api },
        { provide: AgendaActions, useValue: actions },
        {
          provide: AuthService,
          useValue: { hasPermission: (p: string) => permissions.includes(p) },
        },
      ],
    });
  });

  afterEach(() => vi.useRealTimers());

  const settle = async (fixture: ComponentFixture<unknown>) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const open = async (url = '/agenda?year=2026') => {
    const harness = await RouterTestingHarness.create();
    const page = await harness.navigateByUrl(url, AgendaPage);
    await settle(harness.fixture);
    return { harness, page, element: harness.fixture.nativeElement as HTMLElement };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const rows = (element: HTMLElement) =>
    [...element.querySelectorAll('tr.mat-mdc-row:not(.month-row)')] as HTMLTableRowElement[];
  const headings = (element: HTMLElement) =>
    [...element.querySelectorAll('.month-title')].map((heading) => text(heading));

  const draft = (id: string, overrides: Partial<EventSummary> = {}) =>
    summaryOf({ id, title: `Actividad ${id}`, ...overrides });

  it('CA-30: lists the activities of the year grouped by month, in chronological order', async () => {
    items = [
      draft('e1', { start: '2026-10-24T09:00:00', end: '2026-10-24T11:00:00' }),
      draft('e2', { start: '2026-10-27T10:00:00', end: '2026-10-27T11:00:00' }),
      draft('e3', { start: '2026-11-10T22:00:00', end: '2026-11-10T23:00:00' }),
    ];

    const { element } = await open();

    expect(headings(element)).toEqual(['Octubre · 2', 'Noviembre · 1']);
    expect(rows(element).map((row) => text(row.querySelector('.title-cell a')))).toEqual([
      'Actividad e1',
      'Actividad e2',
      'Actividad e3',
    ]);
  });

  it('RF-12: shows date and time, type, title, responsible, state and Outlook state of each activity', async () => {
    items = [
      summaryOf({
        status: 'scheduled',
        impact: 'outage',
        outlook: { status: 'published' },
      }),
    ];

    const { element } = await open();
    const [row] = rows(element);
    const cells = [...row.querySelectorAll('td')].map((cell) => text(cell));

    expect(cells[1]).toBe('sáb 24 oct · 09:00–11:00');
    expect(cells[2]).toBe('Mantenimiento y parches de sistema operativo');
    expect(cells[3]).toContain('Parches de octubre');
    expect(cells[3]).toContain('Impacto: Indisponibilidad');
    expect(cells[4]).toBe('Ana Pérez');
    expect(cells[5]).toContain('Programada');
    expect(cells[6]).toContain('Publicado');
  });

  it('CA-10: marks an activity whose end passed without being closed as Vencida, next to its state', async () => {
    items = [
      draft('e1', { status: 'scheduled', isOverdue: true }),
      draft('e2', { status: 'completed' }),
    ];

    const { element } = await open();
    const [overdue, closed] = rows(element);

    expect(text(overdue.querySelector('app-event-status'))).toContain('Programada');
    expect(text(overdue.querySelector('app-event-status'))).toContain('Vencida');
    expect(text(closed.querySelector('app-event-status'))).not.toContain('Vencida');
  });

  it('CA-11: shows the warnings of an activity with icon and text', async () => {
    items = [draft('e1', { hasWarnings: true }), draft('e2')];

    const { element } = await open();
    const [warned, clean] = rows(element);
    const warning = warned.querySelector('.warning');

    expect(text(warning)).toContain('Advertencias');
    expect(warning?.querySelector('mat-icon')?.textContent).toBe('warning');
    expect(warning?.getAttribute('aria-label')).toBe('Tiene advertencias de solapamiento');
    expect(clean.querySelector('.warning')).toBeNull();
  });

  it('CA-12: shows "Responsable inactivo" for a person that was deactivated', async () => {
    items = [draft('e1', { responsible: { id: 'u9', name: 'Luis Gómez', isActive: false } })];

    const { element } = await open();

    expect(text(rows(element)[0].cells[4])).toBe('Responsable inactivo Luis Gómez');
  });

  it('CA-22: shows a title with markup as literal text', async () => {
    items = [draft('e1', { title: '<script>alert(1)</script> <b>Parches</b>' })];

    const { element } = await open();
    const link = rows(element)[0].querySelector('.title-cell a');

    expect(link?.textContent).toBe('<script>alert(1)</script> <b>Parches</b>');
    expect(element.querySelector('script')).toBeNull();
    expect(link?.querySelector('b')).toBeNull();
  });

  it('shows the zone of the organization next to the period (RNF-9)', async () => {
    const { element } = await open();

    expect(text(element.querySelector('.zone'))).toBe('Horas de America/Bogota');
    expect(text(element.querySelector('.period-title'))).toBe('2026');
  });

  describe('tags (RF-23, RF-25)', () => {
    const withTags = () => {
      items = [
        draft('e1', { tags: ['windows', 'producción', 'iso-27001', 'sql-server', '2026-t4'] }),
      ];
    };

    it('shows the first three tags and "+n" for the rest', async () => {
      withTags();

      const { element } = await open();
      const chips = [
        ...rows(element)[0].querySelectorAll('mat-chip-set[aria-label="Etiquetas"] mat-chip'),
      ];

      expect(chips.map((chip) => text(chip))).toEqual(['windows', 'producción', 'iso-27001', '+2']);
    });

    it('adds the tag to the filter when it is pressed, and the list is asked again with it', async () => {
      withTags();
      const { harness, element } = await open();

      element
        .querySelector<HTMLButtonElement>('button[aria-label="Filtrar por la etiqueta windows"]')!
        .click();
      await settle(harness.fixture);

      await vi.waitFor(() =>
        expect(api.list).toHaveBeenLastCalledWith(
          '2026-01-01',
          '2027-01-01',
          expect.objectContaining({ tags: ['windows'] }),
        ),
      );
      expect(api.summary).toHaveBeenLastCalledWith(
        2026,
        expect.objectContaining({ tags: ['windows'] }),
      );
      expect(
        [...element.querySelectorAll('button')].some((button) =>
          text(button).includes('Limpiar filtros'),
        ),
      ).toBe(true);
    });

    it('does not add a tag that is already in the filter', async () => {
      withTags();
      const { harness, element } = await open('/agenda?year=2026&tags=Windows');
      api.list.mockClear();

      element
        .querySelector<HTMLButtonElement>('button[aria-label="Filtrar por la etiqueta windows"]')!
        .click();
      await settle(harness.fixture);

      expect(api.list).not.toHaveBeenCalled();
    });
  });

  describe('filters in the address (CA-26, CA-34)', () => {
    it('reads the filters from the address and asks the list and the summary with them', async () => {
      await open(
        '/agenda?year=2026&tags=windows&tags=iso-27001&status=overdue&mine=true&search=parches',
      );

      const filters = expect.objectContaining({
        tags: ['windows', 'iso-27001'],
        status: 'overdue',
        mine: true,
        search: 'parches',
      });
      expect(api.list).toHaveBeenLastCalledWith('2026-01-01', '2027-01-01', filters);
      expect(api.summary).toHaveBeenLastCalledWith(2026, filters);
    });

    it('clears every filter with "Limpiar filtros"', async () => {
      const { harness, element } = await open('/agenda?year=2026&tags=windows&status=draft');
      const clear = [...element.querySelectorAll('button')].find((button) =>
        text(button).includes('Limpiar filtros'),
      )!;

      clear.click();
      await settle(harness.fixture);

      await vi.waitFor(() =>
        expect(api.list).toHaveBeenLastCalledWith('2026-01-01', '2027-01-01', {
          type: '',
          status: '',
          responsible: '',
          mine: false,
          tags: [],
          search: '',
        }),
      );
    });

    it('moves the period with the arrows and "Hoy" and keeps it in the address', async () => {
      const { harness, element } = await open('/agenda?year=2026');
      const button = (label: string) =>
        element.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!;

      button('Año siguiente').click();
      await settle(harness.fixture);
      await vi.waitFor(() =>
        expect(api.list).toHaveBeenLastCalledWith('2027-01-01', '2028-01-01', expect.anything()),
      );
      expect(TestBed.inject(Router).url).toContain('year=2027');

      [...element.querySelectorAll('button')].find((b) => text(b) === 'Hoy')!.click();
      await settle(harness.fixture);
      await vi.waitFor(() =>
        expect(api.list).toHaveBeenLastCalledWith('2026-01-01', '2027-01-01', expect.anything()),
      );
    });
  });

  describe('states of the list (§8.4)', () => {
    it('invites to create the first activity when the agenda is empty', async () => {
      const { element } = await open();

      expect(text(element.querySelector('.empty'))).toContain(
        'Todavía no hay actividades planificadas.',
      );
      expect(text(element.querySelector('.empty'))).toContain('Nueva actividad');
      expect(text(element.querySelector('.empty'))).toContain('Nueva serie');
    });

    it('does not offer to create without agenda.create', async () => {
      permissions = ['agenda.read'];

      const { element } = await open();

      expect(text(element.querySelector('.empty'))).toBe(
        'Todavía no hay actividades planificadas.',
      );
    });

    it('says nothing matches the filters, with the button to clear them', async () => {
      const { element } = await open('/agenda?year=2026&search=xyz');

      expect(text(element.querySelector('.empty'))).toContain(
        'Ninguna actividad coincide con los filtros.',
      );
      expect(text(element.querySelector('.empty button'))).toBe('Limpiar filtros');
    });

    it('says there is nothing in a past year without suggesting to plan it', async () => {
      const { element } = await open('/agenda?year=2025');

      expect(text(element.querySelector('.empty'))).toBe('No hay actividades en este periodo.');
    });

    it('warns when there are more activities than can be shown', async () => {
      truncated = true;
      items = [draft('e1')];

      const { element } = await open();

      expect([...element.querySelectorAll('.notice')].map((notice) => text(notice))).toContainEqual(
        expect.stringContaining(
          'Hay más actividades de las que se pueden mostrar. Acota con filtros.',
        ),
      );
    });

    it('shows the error with a retry that asks again', async () => {
      fail = true;
      const { harness, element } = await open();

      expect(text(element.querySelector('[role="alert"]'))).toContain(
        'No se pudo cargar la agenda.',
      );
      fail = false;
      items = [draft('e1')];
      api.list.mockClear();
      [...element.querySelectorAll('button')].find((b) => text(b).includes('Reintentar'))!.click();
      await settle(harness.fixture);

      await vi.waitFor(() => expect(api.list).toHaveBeenCalledTimes(1));
      expect(rows(element).length).toBe(1);
    });
  });

  describe('Outlook (RF-15, RF-18, §8.4)', () => {
    it('says Outlook is not configured when it is off', async () => {
      items = [draft('e1')];

      const { element } = await open();

      expect(text(element.querySelector('.notice'))).toContain('Outlook no configurado');
      expect(text(element)).not.toContain('Sincronizar pendientes con Outlook');
    });

    it('offers to synchronize the pending ones when it is on and the person can publish', async () => {
      outlookEnabled = true;
      items = [draft('e1', { status: 'scheduled', outlook: { status: 'error' } })];

      const { element } = await open();

      expect(element.querySelector('.notice')).toBeNull();
      expect(text(element)).toContain('Sincronizar pendientes con Outlook');
      expect(text(rows(element)[0].cells[6])).toContain('Error');
    });
  });

  describe('exporting to .ics (RF-21)', () => {
    const exportButton = (element: HTMLElement) =>
      [...element.querySelectorAll<HTMLButtonElement>('button')].find(
        (button) => text(button).includes('Exportar .ics') || text(button).includes('Exportando'),
      );
    const hintOf = (page: unknown) => (page as { exportHint: () => string }).exportHint();

    it('offers the button with agenda.read, with a name that says what it exports', async () => {
      permissions = ['agenda.read'];

      const { element } = await open();

      const button = exportButton(element)!;
      expect(text(button)).toBe('download Exportar .ics');
      expect(button.getAttribute('aria-label')).toBe(
        'Exportar .ics de las actividades programadas de 2026',
      );
      expect(button.type).toBe('button');
      expect(button.disabled).toBe(false);
      expect(button.getAttribute('aria-busy')).toBe('false');
      expect(element.querySelector('.page-header')?.contains(button)).toBe(true);
    });

    it('does not offer it without agenda.read (UX only: the API checks it)', async () => {
      permissions = ['agenda.create'];

      const { element } = await open();

      expect(exportButton(element)).toBeUndefined();
    });

    it('exports the year of the list with the active filters', async () => {
      const { element } = await open('/agenda?year=2026&tags=windows&tags=prod&mine=true&type=t1');

      exportButton(element)!.click();

      expect(actions.exportCalendar).toHaveBeenCalledExactlyOnceWith(
        { from: '2026-01-01', to: '2027-01-01', fileName: 'agenda-2026.ics' },
        expect.objectContaining({ tags: ['windows', 'prod'], mine: true, type: 't1' }),
      );
    });

    it('exports the month the list is narrowed to, and says so in its name', async () => {
      const { element } = await open('/agenda?year=2026&month=3&search=parches');

      const button = exportButton(element)!;
      expect(button.getAttribute('aria-label')).toBe(
        'Exportar .ics de las actividades programadas de marzo de 2026',
      );
      button.click();

      expect(actions.exportCalendar).toHaveBeenCalledExactlyOnceWith(
        { from: '2026-03-01', to: '2026-04-01', fileName: 'agenda-2026-03.ics' },
        expect.objectContaining({ search: 'parches' }),
      );
    });

    it('exports the year that is shown, not the current one', async () => {
      const { element } = await open('/agenda?year=2027');

      exportButton(element)!.click();

      expect(actions.exportCalendar).toHaveBeenCalledWith(
        expect.objectContaining({ from: '2027-01-01', to: '2028-01-01' }),
        expect.anything(),
      );
    });

    it('shows it is working: disabled, busy and with another name, and ignores a second press', async () => {
      const pending = new Subject<boolean>();
      actions.exportCalendar.mockReturnValue(pending);
      const { harness, element } = await open();

      exportButton(element)!.click();
      await settle(harness.fixture);

      const busy = exportButton(element)!;
      expect(text(busy)).toBe('download Exportando…');
      expect(busy.disabled).toBe(true);
      expect(busy.getAttribute('aria-busy')).toBe('true');
      expect(busy.getAttribute('aria-label')).toBe('Exportando las actividades programadas…');
      busy.click();
      expect(actions.exportCalendar).toHaveBeenCalledTimes(1);

      pending.next(true);
      pending.complete();
      await settle(harness.fixture);

      const done = exportButton(element)!;
      expect(text(done)).toBe('download Exportar .ics');
      expect(done.disabled).toBe(false);
      expect(done.getAttribute('aria-busy')).toBe('false');
    });

    it('is ready to try again after a failure', async () => {
      actions.exportCalendar.mockReturnValue(of(false));
      const { harness, element } = await open();

      exportButton(element)!.click();
      await settle(harness.fixture);
      exportButton(element)!.click();

      expect(exportButton(element)!.disabled).toBe(false);
      expect(actions.exportCalendar).toHaveBeenCalledTimes(2);
    });

    it('does not touch the list: exporting reloads nothing and changes no filter', async () => {
      const { harness, element } = await open('/agenda?year=2026&tags=windows');
      api.list.mockClear();
      api.summary.mockClear();

      exportButton(element)!.click();
      await settle(harness.fixture);

      expect(api.list).not.toHaveBeenCalled();
      expect(api.summary).not.toHaveBeenCalled();
      expect(TestBed.inject(Router).url).toContain('tags=windows');
    });

    it('warns in its hint that only scheduled activities are exported, and that the state filter does not apply', async () => {
      const draftFilter = await open('/agenda?year=2026&status=draft');
      expect(hintOf(draftFilter.page)).toBe(
        'Descarga las actividades programadas de 2026, con los filtros activos, en un archivo .ics para Outlook u otro calendario. El filtro de estado no se aplica: solo se exportan las programadas.',
      );
    });

    it.each(['/agenda?year=2026', '/agenda?year=2026&status=scheduled'])(
      'has no note about the state when there is no state filter or it is already "Programada" (%s)',
      async (url) => {
        const { page } = await open(url);

        expect(hintOf(page)).toBe(
          'Descarga las actividades programadas de 2026, con los filtros activos, en un archivo .ics para Outlook u otro calendario.',
        );
      },
    );
  });

  describe('permissions (UX only, CA-20)', () => {
    it('hides the creation, the types and the scheduling without their permissions', async () => {
      permissions = ['agenda.read'];
      items = [draft('e1')];

      const { element } = await open();

      expect(text(element.querySelector('.page-header'))).not.toContain('Nueva actividad');
      expect(text(element.querySelector('.page-header'))).not.toContain('Tipos de actividad');
      expect(element.querySelector('.batch')).toBeNull();
      expect(element.querySelector('mat-checkbox')).toBeNull();
    });

    it('shows the creation, the types and the scheduling with their permissions', async () => {
      items = [draft('e1')];

      const { element } = await open();
      const header = text(element.querySelector('.page-header'));

      expect(header).toContain('Nueva actividad');
      expect(header).toContain('Nueva serie');
      expect(header).toContain('Tipos de actividad');
      expect(element.querySelector('.batch')).not.toBeNull();
    });
  });

  describe('scheduling in a batch (RF-9)', () => {
    const batchButton = (element: HTMLElement) =>
      [...element.querySelectorAll('button')].find((b) =>
        text(b).includes('Programar seleccionadas'),
      )!;
    const checkboxes = (element: HTMLElement) => [
      ...element.querySelectorAll<HTMLInputElement>('mat-checkbox input[type="checkbox"]'),
    ];

    it('lets the person pick only drafts and schedules the picked ones after a confirmation', async () => {
      items = [draft('e1'), draft('e2'), draft('e3', { status: 'scheduled' })];
      const { harness, element } = await open();

      // header + two drafts: the scheduled one has no checkbox
      expect(checkboxes(element).length).toBe(3);
      expect(batchButton(element).disabled).toBe(true);

      checkboxes(element)[1].click();
      checkboxes(element)[2].click();
      await settle(harness.fixture);
      expect(text(batchButton(element))).toContain('(2)');
      batchButton(element).click();

      expect(actions.schedule).toHaveBeenCalledWith(
        [expect.objectContaining({ id: 'e1' }), expect.objectContaining({ id: 'e2' })],
        false,
      );
    });

    it('selects all the drafts from the header', async () => {
      items = [draft('e1'), draft('e2')];
      const { harness, element } = await open();

      checkboxes(element)[0].click();
      await settle(harness.fixture);

      expect(text(batchButton(element))).toContain('(2)');
    });

    it('does not allow more than 50 at once and says so', async () => {
      items = Array.from({ length: 51 }, (_, index) => draft(`e${index}`));
      const { harness, element } = await open();

      checkboxes(element)[0].click();
      await settle(harness.fixture);

      expect(batchButton(element).disabled).toBe(true);
      expect(text(element.querySelector('.batch'))).toContain(
        'Selecciona hasta 50 actividades a la vez.',
      );
    });

    it('reloads the list when something was scheduled', async () => {
      items = [draft('e1')];
      const { harness, element } = await open();
      api.list.mockClear();

      checkboxes(element)[1].click();
      await settle(harness.fixture);
      batchButton(element).click();
      await settle(harness.fixture);

      await vi.waitFor(() => expect(api.list).toHaveBeenCalledTimes(1));
    });
  });

  describe('row menu', () => {
    const menuItems = async (
      fixture: ComponentFixture<unknown>,
      element: HTMLElement,
      title: string,
    ) => {
      element
        .querySelector<HTMLButtonElement>(`button[aria-label="Acciones de ${title}"]`)!
        .click();
      await settle(fixture);
      return [...document.querySelectorAll('.mat-mdc-menu-item')].map((item) =>
        text(item.querySelector('span')),
      );
    };

    afterEach(() =>
      document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = '')),
    );

    it('offers what a draft allows', async () => {
      items = [draft('e1')];
      const { harness, element } = await open();

      expect(await menuItems(harness.fixture, element, 'Actividad e1')).toEqual([
        'Ver',
        'Editar',
        'Programar',
        'Eliminar borrador',
      ]);
    });

    it('offers what a scheduled activity allows, with "Sincronizar de nuevo" when Outlook has not confirmed it', async () => {
      outlookEnabled = true;
      items = [draft('e1', { status: 'scheduled', outlook: { status: 'error' } })];
      const { harness, element } = await open();

      expect(await menuItems(harness.fixture, element, 'Actividad e1')).toEqual([
        'Ver',
        'Editar',
        'Cancelar',
        'Registrar ejecución',
        'Sincronizar de nuevo',
      ]);
    });

    it('P-12: does not offer "Editar" on a scheduled activity to whom cannot publish', async () => {
      permissions = ['agenda.read', 'agenda.update'];
      items = [draft('e1'), draft('e2', { status: 'scheduled' })];
      const { harness, element } = await open();

      expect(await menuItems(harness.fixture, element, 'Actividad e1')).toEqual([
        'Ver',
        'Editar',
        'Eliminar borrador',
      ]);
      document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));
      expect(await menuItems(harness.fixture, element, 'Actividad e2')).toEqual(['Ver']);
    });

    it('does not offer "Sincronizar de nuevo" once Outlook confirmed the activity', async () => {
      outlookEnabled = true;
      items = [draft('e2', { status: 'scheduled', outlook: { status: 'published' } })];
      const { harness, element } = await open();

      expect(await menuItems(harness.fixture, element, 'Actividad e2')).toEqual([
        'Ver',
        'Editar',
        'Cancelar',
        'Registrar ejecución',
      ]);
    });

    it('RN-8: disables "Registrar ejecución" until the scheduled start', async () => {
      items = [
        draft('e1', {
          status: 'scheduled',
          startUtc: '2026-10-24T14:00:00Z',
          endUtc: '2026-10-24T16:00:00Z',
        }),
        draft('e2', {
          status: 'scheduled',
          startUtc: '2026-10-10T14:00:00Z',
          endUtc: '2026-10-10T16:00:00Z',
        }),
      ];
      const { harness, element } = await open();

      await menuItems(harness.fixture, element, 'Actividad e1');
      const future = [...document.querySelectorAll<HTMLButtonElement>('.mat-mdc-menu-item')].find(
        (item) => text(item).includes('Registrar ejecución'),
      );
      expect(future?.disabled).toBe(true);
      document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = ''));

      await menuItems(harness.fixture, element, 'Actividad e2');
      const past = [...document.querySelectorAll<HTMLButtonElement>('.mat-mdc-menu-item')].find(
        (item) => text(item).includes('Registrar ejecución'),
      );
      expect(past?.disabled).toBe(false);
    });

    it('offers only "Ver" for a completed activity', async () => {
      items = [draft('e1', { status: 'completed' })];
      const { harness, element } = await open();

      expect(await menuItems(harness.fixture, element, 'Actividad e1')).toEqual(['Ver']);
    });
  });

  describe('calendar views (RF-12, CA-31 to CA-34)', () => {
    const calendarOf = (fixture: ComponentFixture<unknown>) =>
      fixture.debugElement.query(By.directive(AgendaCalendar));
    const toggleLabels = (element: HTMLElement) =>
      [...element.querySelectorAll('mat-button-toggle')].map((toggle) => text(toggle));
    const checkedToggle = (element: HTMLElement) =>
      text(element.querySelector('mat-button-toggle.mat-button-toggle-checked'));
    const step = (element: HTMLElement, label: string) =>
      [...element.querySelectorAll<HTMLButtonElement>('.period button')].find(
        (button) => button.getAttribute('aria-label') === label || text(button) === label,
      )!;
    const urlOf = () => TestBed.inject(Router).url;

    it('offers the four views, in the order of RF-12, with the one in the address chosen', async () => {
      const { element } = await open('/agenda?view=month&year=2026&month=10');

      expect(toggleLabels(element)).toEqual(['Lista del año', 'Año', 'Mes', 'Semana']);
      expect(checkedToggle(element)).toContain('Mes');
    });

    it('draws the month, asks for the whole grid it shows and does not ask for the summary of the year (CA-32)', async () => {
      items = [draft('e1', { start: '2026-10-24T09:00:00', end: '2026-10-24T11:00:00' })];

      const { harness, element } = await open('/agenda?view=month&year=2026&month=10');

      expect(api.list).toHaveBeenLastCalledWith('2026-09-28', '2026-11-02', expect.anything());
      expect(api.summary).not.toHaveBeenCalled();
      const calendar = calendarOf(harness.fixture).componentInstance as AgendaCalendar;
      expect(calendar.view()).toBe('month');
      expect(calendar.date()).toBe('2026-10-01');
      expect(calendar.events().map((event) => event.id)).toEqual(['e1']);
      expect(calendar.timeZone()).toBe('America/Bogota');
      expect(element.querySelector('table')).toBeNull();
      expect(element.querySelector('app-year-summary')).toBeNull();
      expect(element.querySelector('.batch')).toBeNull();
      expect(text(element.querySelector('.period-title'))).toBe('Octubre de 2026');
    });

    it('draws the week of the day in the address and asks for that week (CA-33)', async () => {
      const { harness, element } = await open('/agenda?view=week&date=2026-10-21');

      expect(api.list).toHaveBeenLastCalledWith('2026-10-19', '2026-10-26', expect.anything());
      const calendar = calendarOf(harness.fixture).componentInstance as AgendaCalendar;
      expect([calendar.view(), calendar.date()]).toEqual(['week', '2026-10-21']);
      expect(text(element.querySelector('.period-title'))).toBe('19 oct – 25 oct 2026');
    });

    it('draws the year and asks for the year (CA-31)', async () => {
      const { harness } = await open('/agenda?view=year&year=2027');

      expect(api.list).toHaveBeenLastCalledWith('2027-01-01', '2028-01-01', expect.anything());
      expect((calendarOf(harness.fixture).componentInstance as AgendaCalendar).date()).toBe(
        '2027-01-01',
      );
    });

    it('does not load the calendar for the year list (the calendar is a lazy part)', async () => {
      const { harness } = await open('/agenda?year=2026');

      expect(calendarOf(harness.fixture)).toBeNull();
      expect(api.summary).toHaveBeenCalled();
    });

    it('moves the period with Anterior, Hoy and Siguiente of each view, named for what they move (CA-34)', async () => {
      const { harness, element } = await open('/agenda?view=month&year=2026&month=10');

      step(element, 'Mes siguiente').click();
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=month&month=11');

      step(element, 'Mes anterior').click();
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=month&month=10');
      step(element, 'Mes anterior').click();
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=month&month=9');
      step(element, 'Hoy').click();
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=month&month=10');

      await harness.navigateByUrl('/agenda?view=week&date=2026-10-21', AgendaPage);
      await settle(harness.fixture);
      step(element, 'Semana siguiente').click();
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=week&date=2026-10-28');
      step(element, 'Semana anterior').click();
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=week&date=2026-10-21');
      step(element, 'Hoy').click();
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=week&date=2026-10-18');

      await harness.navigateByUrl('/agenda?view=year&year=2026', AgendaPage);
      await settle(harness.fixture);
      step(element, 'Año siguiente').click();
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=year&year=2027');
    });

    it('keeps the filters when the view changes and when the period moves (CA-34)', async () => {
      const { harness, element } = await open('/agenda?year=2026&tags=windows&mine=true&type=t1');

      const toggles = [...element.querySelectorAll<HTMLButtonElement>('mat-button-toggle button')];
      toggles.find((button) => text(button) === 'Mes')!.click();
      await settle(harness.fixture);
      expect(urlOf()).toContain('view=month');
      for (const part of ['tags=windows', 'mine=true', 'type=t1']) {
        expect(urlOf()).toContain(part);
      }

      step(element, 'Mes siguiente').click();
      await settle(harness.fixture);
      for (const part of ['view=month', 'tags=windows', 'mine=true', 'type=t1']) {
        expect(urlOf()).toContain(part);
      }
      expect(api.list).toHaveBeenLastCalledWith(
        '2026-10-26',
        '2026-12-07',
        expect.objectContaining({ tags: ['windows'], mine: true, type: 't1' }),
      );
    });

    it('goes to the week of a day chosen in the year and to a month chosen by its title (CA-31)', async () => {
      const { harness } = await open('/agenda?view=year&year=2026');
      const calendar = calendarOf(harness.fixture).componentInstance as AgendaCalendar;

      calendar.positionChange.emit({ view: 'week', date: '2026-11-12' });
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=week&date=2026-11-12');

      calendar.positionChange.emit({ view: 'month', date: '2027-02-01' });
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=month&year=2027&month=2');
    });

    it('opens the detail of the activity chosen in the calendar (CA-34)', async () => {
      const { harness } = await open('/agenda?view=month&year=2026&month=10');

      (calendarOf(harness.fixture).componentInstance as AgendaCalendar).eventOpen.emit('e7');
      await settle(harness.fixture);

      expect(urlOf()).toBe('/agenda/events/e7');
    });

    it('opens "Nueva actividad" with the day and the hour chosen in the calendar (RF-12b, CA-34)', async () => {
      const { harness } = await open('/agenda?view=week&date=2026-10-21');
      const calendar = calendarOf(harness.fixture).componentInstance as AgendaCalendar;

      calendar.slotChoose.emit({ date: '2026-10-21', time: '14:30' });
      await settle(harness.fixture);

      expect(urlOf()).toBe('/agenda/events/new?date=2026-10-21&time=14:30');
    });

    it('offers to plan from an empty day with agenda.create (RF-12b)', async () => {
      const { harness } = await open('/agenda?view=month&year=2026&month=10');

      expect((calendarOf(harness.fixture).componentInstance as AgendaCalendar).canCreate()).toBe(
        true,
      );
    });

    it('does not offer it without agenda.create (RF-12b, UX only)', async () => {
      permissions = ['agenda.read'];

      const { harness } = await open('/agenda?view=month&year=2026&month=10');

      expect((calendarOf(harness.fixture).componentInstance as AgendaCalendar).canCreate()).toBe(
        false,
      );
    });

    it('does not offer it for a past year (RN-16)', async () => {
      const { harness } = await open('/agenda?view=year&year=2024');

      expect((calendarOf(harness.fixture).componentInstance as AgendaCalendar).canCreate()).toBe(
        false,
      );
    });

    it('warns that there are more activities than can be shown, in the calendars too (CA-31)', async () => {
      truncated = true;

      const { element } = await open('/agenda?view=year&year=2026');

      expect([...element.querySelectorAll('.notice[role=status]')].map(text).join(' ')).toContain(
        'Hay más actividades de las que se pueden mostrar. Acota con filtros.',
      );
    });

    it('says when a period has no activities and still draws the calendar (§8.4, CA-32)', async () => {
      const { harness, element } = await open('/agenda?view=month&year=2026&month=10');

      expect(text(element.querySelector('p.supporting-text[role=status]'))).toBe(
        'No hay actividades en este periodo.',
      );
      expect(calendarOf(harness.fixture)).not.toBeNull();
    });

    it('says when the filters leave a period without activities and offers to clear them', async () => {
      const { harness, element } = await open('/agenda?view=month&year=2026&month=10&tags=windows');

      expect(text(element.querySelector('p.supporting-text[role=status]'))).toBe(
        'Ninguna actividad coincide con los filtros. Limpiar filtros',
      );
      element.querySelector<HTMLButtonElement>('p.supporting-text[role=status] button')!.click();
      await settle(harness.fixture);
      expect(urlOf()).toBe('/agenda?view=month&month=10');
    });

    it('exports the week that is shown, with its name, to the file of that week (RF-21)', async () => {
      const { element } = await open('/agenda?view=week&date=2026-10-21&tags=windows');
      const button = [...element.querySelectorAll<HTMLButtonElement>('button')].find((candidate) =>
        text(candidate).includes('Exportar .ics'),
      )!;

      expect(button.getAttribute('aria-label')).toBe(
        'Exportar .ics de las actividades programadas de la semana del 19 de octubre de 2026',
      );
      button.click();

      expect(actions.exportCalendar).toHaveBeenCalledExactlyOnceWith(
        { from: '2026-10-19', to: '2026-10-26', fileName: 'agenda-semana-2026-10-19.ics' },
        expect.objectContaining({ tags: ['windows'] }),
      );
    });

    it('exports the month that is shown in the month view', async () => {
      const { element } = await open('/agenda?view=month&year=2026&month=11');
      const button = [...element.querySelectorAll<HTMLButtonElement>('button')].find((candidate) =>
        text(candidate).includes('Exportar .ics'),
      )!;

      button.click();

      expect(actions.exportCalendar).toHaveBeenCalledExactlyOnceWith(
        { from: '2026-11-01', to: '2026-12-01', fileName: 'agenda-2026-11.ics' },
        expect.anything(),
      );
    });

    it('opens on the current year of the organization, not of the browser (RNF-9)', async () => {
      // 02:00 UTC on 1 January 2027: still 31 December 2026 (21:00) in Bogotá.
      vi.setSystemTime(new Date('2027-01-01T02:00:00Z'));

      const { element } = await open('/agenda');

      expect(text(element.querySelector('.period-title'))).toBe('2026');
      expect(api.list).toHaveBeenLastCalledWith('2026-01-01', '2027-01-01', expect.anything());
    });

    it('falls back to the year list for a view that does not exist', async () => {
      const { element } = await open('/agenda?view=decade&year=2026');

      expect(checkedToggle(element)).toContain('Lista del año');
    });
  });

  it('keeps the loading state until the list arrives', async () => {
    const pending = new Subject<EventList>();
    api.list.mockReturnValue(pending);

    const { element } = await open();

    expect(
      element.querySelector('mat-progress-bar[aria-label="Cargando actividades"]'),
    ).not.toBeNull();
    pending.next({
      items: [],
      truncated: false,
      timeZone: 'America/Bogota',
      outlookEnabled: false,
    });
    pending.complete();
  });
});
