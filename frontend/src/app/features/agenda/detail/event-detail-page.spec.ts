import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { Subject, of, throwError } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { AgendaApi } from '../data/agenda-api';
import { detailOf, versionedOf } from '../data/agenda-fixtures';
import { EventDetail } from '../data/agenda.models';
import { AgendaActions } from '../events/agenda-actions';
import { EventDetailPage } from './event-detail-page';

const ALL = [
  'agenda.read',
  'agenda.create',
  'agenda.update',
  'agenda.publish',
  'agenda.complete',
  'agenda.types.manage',
];

const FUTURE = { startUtc: '2099-01-10T14:00:00Z', endUtc: '2099-01-10T15:00:00Z' };
const PAST = { startUtc: '2020-01-10T14:00:00Z', endUtc: '2020-01-10T15:00:00Z' };

describe('EventDetailPage (RF-14, RF-24, §8.4)', () => {
  let event: EventDetail;
  let permissions: string[];
  const api = { getEvent: vi.fn(), setTags: vi.fn(), listTags: vi.fn() };
  const actions = {
    schedule: vi.fn(),
    cancel: vi.fn(),
    complete: vi.fn(),
    deleteDraft: vi.fn(),
    sync: vi.fn(),
  };
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    event = detailOf();
    permissions = ALL;
    api.getEvent.mockReset().mockImplementation(() => of({ event, etag: `"${event.revision}"` }));
    api.setTags.mockReset().mockImplementation((_id: string, tags: readonly string[]) => of(tags));
    api.listTags.mockReset().mockReturnValue(of([]));
    for (const action of Object.values(actions)) {
      action.mockReset();
    }
    actions.schedule.mockReturnValue(of(true));
    actions.deleteDraft.mockReturnValue(of(true));
    actions.sync.mockReturnValue(of({ status: 'published' }));
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AgendaApi, useValue: api },
        { provide: AgendaActions, useValue: actions },
        {
          provide: AuthService,
          useValue: { hasPermission: (p: string) => permissions.includes(p) },
        },
      ],
    });
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  });

  const settle = async (fixture: ComponentFixture<unknown>) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const render = async () => {
    const fixture = TestBed.createComponent(EventDetailPage);
    fixture.componentRef.setInput('id', 'e1');
    await settle(fixture);
    return { fixture, element: fixture.nativeElement as HTMLElement };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const find = (element: HTMLElement, label: string) =>
    [...element.querySelectorAll<HTMLElement>('button, a')].find((b) => text(b).includes(label));
  const actionLabels = (element: HTMLElement) =>
    [...element.querySelectorAll('.actions > button, .actions > a, .actions > span > button')].map(
      (b) => text(b).replace(/^[a-z_]+\s?/, ''),
    );
  const card = (element: HTMLElement, title: string) =>
    [...element.querySelectorAll('mat-card')].find(
      (c) => text(c.querySelector('mat-card-title')) === title,
    ) as HTMLElement | undefined;
  /** The value of a term of a definition list (`dt` → next `dd`). */
  const fact = (scope: Element | undefined | null, term: string) => {
    const dt = [...(scope?.querySelectorAll('dt') ?? [])].find((d) => text(d) === term);
    return text(dt?.nextElementSibling);
  };

  describe('data (RF-14)', () => {
    it('shows every datum of the activity', async () => {
      event = detailOf({
        participants: ['ana@example.com', 'luis@example.com'],
        impact: 'degradation',
        reminderMinutes: 1440,
        series: {
          unit: 'month',
          interval: 1,
          anchor: { kind: 'nth-weekday', dayOfMonth: null, nth: 2, weekday: 'tuesday' },
          startsOn: '2026-10-24',
          endsOn: '2026-12-31',
          count: null,
        },
        seriesId: 's1',
      });

      const { element } = await render();
      const data = card(element, 'Datos');

      expect(text(element.querySelector('h1'))).toBe('Parches de octubre');
      expect(fact(data, 'Tipo')).toBe('Mantenimiento y parches de sistema operativo');
      expect(fact(data, 'Cuándo')).toBe('sáb 24 oct · 09:00–11:00');
      expect(fact(data, 'Responsable')).toBe('Ana Pérez');
      expect([...data!.querySelectorAll('dd ul li')].map((item) => text(item))).toEqual([
        'ana@example.com',
        'luis@example.com',
      ]);
      expect(fact(data, 'Avisar al equipo')).toBe('Sí');
      expect(fact(data, 'Impacto')).toBe('Degradación');
      expect(fact(data, 'Alcance')).toBe('Servidores Windows');
      expect(fact(data, 'Lugar')).toBe('Remoto');
      expect(fact(data, 'Recordatorio')).toBe('1 día antes');
      expect(fact(data, 'Serie')).toBe(
        'Cada mes, el segundo martes, hasta el 31 de diciembre de 2026',
      );
      expect(text(data)).toContain('Aplicar los parches del mes');
      expect(text(element)).toContain('zona America/Bogota');
    });

    it('says what is missing instead of leaving blanks', async () => {
      event = detailOf({ participants: [], scope: null, location: null, description: null });

      const { element } = await render();
      const data = card(element, 'Datos');

      expect(fact(data, 'Participantes')).toBe('Sin participantes');
      expect(fact(data, 'Alcance')).toBe('Sin indicar');
      expect(fact(data, 'Lugar')).toBe('Sin indicar');
      expect(text(data)).not.toContain('Descripción');
    });

    it('CA-12: shows "Responsable inactivo" with the name', async () => {
      event = detailOf({ responsible: { id: 'u9', name: 'Luis Gómez', isActive: false } });

      const { element } = await render();

      expect(fact(card(element, 'Datos'), 'Responsable')).toBe('Responsable inactivo Luis Gómez');
    });

    it('CA-10: marks it as Vencida next to its state', async () => {
      event = detailOf({ status: 'scheduled', isOverdue: true });

      const { element } = await render();

      expect(text(element.querySelector('app-event-status'))).toContain('Programada');
      expect(text(element.querySelector('app-event-status'))).toContain('Vencida');
    });

    it('CA-11: shows the warnings with icon and text', async () => {
      event = detailOf({
        warnings: [{ kind: 'impact-overlap', eventId: 'e2', title: 'Migración de red' }],
      });

      const { element } = await render();

      expect(text(element.querySelector('app-warning-list li span'))).toBe(
        'Se solapa con «Migración de red», que también tiene impacto.',
      );
      expect(text(element.querySelector('app-warning-list mat-icon'))).toBe('warning');
    });

    it('shows the reason of a cancelled activity', async () => {
      event = detailOf({ status: 'cancelled', cancelReason: 'Cambio de ventana' });

      const { element } = await render();

      expect(fact(card(element, 'Datos'), 'Motivo de la cancelación')).toBe('Cambio de ventana');
    });

    it('shows who created and who changed it', async () => {
      event = detailOf({ updatedAt: '2026-10-18T15:00:00Z', updatedBy: 'Ana Pérez' });

      const { element } = await render();
      const record = card(element, 'Registro');

      expect(fact(record, 'Creada')).toContain('por Luis Gómez');
      expect(fact(record, 'Modificada')).toContain('por Ana Pérez');
    });

    it('CA-22: shows everything a person typed as literal text', async () => {
      const markup = '<script>alert(1)</script><img src=x onerror=alert(2)>';
      event = detailOf({
        title: markup,
        description: `<b>${markup}</b>`,
        scope: markup,
        location: markup,
        tags: [markup],
        status: 'completed',
        completion: {
          result: 'succeeded',
          executedAt: '2026-10-24T14:00:00Z',
          completedAt: '2026-10-24T16:00:00Z',
          completedBy: markup,
          notes: markup,
          evidenceLinks: [{ label: markup, url: 'https://example.com/e' }],
        },
      });

      const { element } = await render();

      expect(text(element.querySelector('h1'))).toBe(markup);
      expect(element.querySelector('script')).toBeNull();
      expect(element.querySelector('img')).toBeNull();
      expect(element.querySelector('b')).toBeNull();
      expect(text(card(element, 'Datos'))).toContain(`<b>${markup}</b>`);
      expect(fact(card(element, 'Ejecución'), 'Notas')).toBe(markup);
    });
  });

  describe('Outlook (RF-14, RF-18)', () => {
    it('says Outlook is not configured when it is off', async () => {
      event = detailOf({ outlookEnabled: false });

      const { element } = await render();
      const outlook = card(element, 'Outlook');

      expect(text(outlook)).toContain(
        'Outlook no configurado: programar solo cambia el estado de la actividad.',
      );
      expect(fact(outlook, 'Estado')).toContain('No publicado');
    });

    it('opens a published activity in Outlook with a safe link', async () => {
      event = detailOf({
        status: 'scheduled',
        outlookEnabled: true,
        outlook: {
          status: 'published',
          errorCode: null,
          statusAt: '2026-10-18T15:00:00Z',
          webLink: 'https://outlook.office.com/calendar/item/abc',
        },
      });

      const { element } = await render();
      const link = card(element, 'Outlook')!.querySelector('a');

      expect(fact(card(element, 'Outlook'), 'Estado')).toContain('Publicado');
      expect(link?.getAttribute('href')).toBe('https://outlook.office.com/calendar/item/abc');
      expect(link?.getAttribute('target')).toBe('_blank');
      expect(link?.getAttribute('rel')).toBe('noopener noreferrer');
    });

    it('does not draw a link that is not http or https', async () => {
      event = detailOf({
        outlookEnabled: true,
        outlook: {
          status: 'published',
          errorCode: null,
          statusAt: null,
          webLink: 'javascript:alert(1)',
        },
      });

      const { element } = await render();

      expect(card(element, 'Outlook')!.querySelector('a')).toBeNull();
    });

    it('shows the failure with a normalized message and code, never the raw detail', async () => {
      event = detailOf({
        status: 'scheduled',
        outlookEnabled: true,
        outlook: { status: 'error', errorCode: 'graph_unavailable', statusAt: null, webLink: null },
      });

      const { element } = await render();
      const error = fact(card(element, 'Outlook'), 'Error');

      expect(error).toContain('No se pudo publicar en Outlook.');
      expect(error).toContain('Outlook no está disponible.');
      expect(error).toContain('graph_unavailable');
    });
  });

  describe('execution record (RF-11, RN-10)', () => {
    const completed = () =>
      detailOf({
        status: 'completed',
        completion: {
          result: 'partial',
          executedAt: '2026-10-24T14:00:00Z',
          completedAt: '2026-10-24T16:00:00Z',
          completedBy: 'Ana Pérez',
          notes: 'Faltó el servidor 4',
          evidenceLinks: [
            { label: 'Informe', url: 'https://example.com/informe' },
            { label: 'Trampa', url: 'javascript:alert(1)' },
          ],
        },
      });

    it('shows the result, the notes and the links of evidence', async () => {
      event = completed();

      const { element } = await render();
      const record = card(element, 'Ejecución');

      expect(fact(record, 'Resultado')).toBe('Realizada parcialmente');
      expect(fact(record, 'Notas')).toBe('Faltó el servidor 4');
      expect(fact(record, 'Registrada')).toContain('por Ana Pérez');
      expect(fact(record, 'Fecha y hora reales')).toContain('09:00');
    });

    it('opens the links with rel="noopener noreferrer" and draws only http and https ones as links', async () => {
      event = completed();

      const { element } = await render();
      const links = [...card(element, 'Ejecución')!.querySelectorAll('a')];

      expect(links.length).toBe(1);
      expect(links[0].getAttribute('href')).toBe('https://example.com/informe');
      expect(links[0].getAttribute('rel')).toBe('noopener noreferrer');
      expect(text(card(element, 'Ejecución'))).toContain('Trampa');
    });

    it('has no execution card before the activity is completed', async () => {
      const { element } = await render();

      expect(card(element, 'Ejecución')).toBeUndefined();
    });
  });

  describe('actions by state and permission (RF-8, §7, CA-20)', () => {
    it('a draft can be edited, scheduled and deleted', async () => {
      event = detailOf({ status: 'draft' });

      const { element } = await render();

      expect(actionLabels(element)).toEqual(['Editar', 'Programar', 'Eliminar borrador']);
    });

    it('a scheduled activity that has started can be recorded, cancelled and edited', async () => {
      event = detailOf({ status: 'scheduled', ...PAST });

      const { element } = await render();

      expect(actionLabels(element)).toEqual([
        'Editar',
        'Registrar ejecución',
        'Cancelar actividad',
      ]);
      expect((find(element, 'Registrar ejecución') as HTMLButtonElement).disabled).toBe(false);
    });

    it('does not let the execution be recorded before the scheduled start (RN-8)', async () => {
      event = detailOf({ status: 'scheduled', ...FUTURE });

      const { element } = await render();

      expect((find(element, 'Registrar ejecución') as HTMLButtonElement).disabled).toBe(true);
    });

    it('a completed activity only allows editing its notes and evidence', async () => {
      event = detailOf({
        status: 'completed',
        completion: {
          result: 'succeeded',
          executedAt: '2026-10-24T14:00:00Z',
          completedAt: '2026-10-24T16:00:00Z',
          completedBy: null,
          notes: null,
          evidenceLinks: [],
        },
      });

      const { element } = await render();

      expect(actionLabels(element)).toEqual(['Editar notas y evidencia']);
    });

    it('a cancelled activity allows nothing (RN-9)', async () => {
      event = detailOf({ status: 'cancelled', cancelReason: 'x' });

      const { element } = await render();

      expect(actionLabels(element)).toEqual([]);
    });

    it('offers the series edit only from an open occurrence of a series', async () => {
      event = detailOf({ status: 'draft', seriesId: 's1', ...FUTURE });
      const open = await render();
      expect(actionLabels(open.element)).toContain('Editar la serie');

      event = detailOf({ status: 'scheduled', seriesId: 's1', ...PAST });
      const started = await render();
      expect(actionLabels(started.element)).not.toContain('Editar la serie');

      event = detailOf({ status: 'draft', seriesId: null, ...FUTURE });
      const single = await render();
      expect(actionLabels(single.element)).not.toContain('Editar la serie');
    });

    it('offers "Sincronizar de nuevo" only with Outlook on, when it has not confirmed, and with agenda.publish', async () => {
      event = detailOf({
        status: 'scheduled',
        outlookEnabled: true,
        outlook: { status: 'error', errorCode: 'graph_timeout', statusAt: null, webLink: null },
      });
      expect(actionLabels((await render()).element)).toContain('Sincronizar de nuevo');

      event = detailOf({
        status: 'scheduled',
        outlookEnabled: true,
        outlook: { status: 'published', errorCode: null, statusAt: null, webLink: null },
      });
      expect(actionLabels((await render()).element)).not.toContain('Sincronizar de nuevo');

      event = detailOf({
        status: 'scheduled',
        outlookEnabled: false,
        outlook: { status: 'not-published', errorCode: null, statusAt: null, webLink: null },
      });
      expect(actionLabels((await render()).element)).not.toContain('Sincronizar de nuevo');

      permissions = ['agenda.read', 'agenda.update'];
      event = detailOf({
        status: 'scheduled',
        outlookEnabled: true,
        outlook: { status: 'error', errorCode: 'graph_timeout', statusAt: null, webLink: null },
      });
      expect(actionLabels((await render()).element)).not.toContain('Sincronizar de nuevo');
    });

    it('offers nothing to a person that can only read', async () => {
      permissions = ['agenda.read'];
      event = detailOf({ status: 'scheduled', ...PAST });

      const { element } = await render();

      expect(actionLabels(element)).toEqual([]);
    });

    it('separates update from publish: who edits cannot schedule, cancel or change what is scheduled (P-12)', async () => {
      permissions = ['agenda.read', 'agenda.update'];
      event = detailOf({ status: 'draft' });
      expect(actionLabels((await render()).element)).toEqual(['Editar', 'Eliminar borrador']);

      event = detailOf({ status: 'scheduled', ...PAST });
      expect(actionLabels((await render()).element)).toEqual([]);

      permissions = ['agenda.read', 'agenda.update', 'agenda.publish'];
      expect(actionLabels((await render()).element)).toContain('Editar');
    });
  });

  describe('running the actions', () => {
    it('schedules after the confirmation and reloads', async () => {
      event = detailOf({ status: 'draft', outlookEnabled: true });
      const { fixture, element } = await render();
      api.getEvent.mockClear();

      find(element, 'Programar')!.click();
      await settle(fixture);

      expect(actions.schedule).toHaveBeenCalledWith([expect.objectContaining({ id: 'e1' })], true);
      expect(api.getEvent).toHaveBeenCalledTimes(1);
    });

    it('shows the activity as the cancel returns it, without asking again', async () => {
      event = detailOf({ status: 'scheduled', ...PAST });
      actions.cancel.mockReturnValue(
        of(versionedOf({ status: 'cancelled', cancelReason: 'Cambio de ventana' })),
      );
      const { fixture, element } = await render();
      api.getEvent.mockClear();

      find(element, 'Cancelar actividad')!.click();
      await settle(fixture);

      expect(actions.cancel).toHaveBeenCalledWith(expect.objectContaining({ id: 'e1' }), false);
      expect(text(element.querySelector('app-event-status'))).toContain('Cancelada');
      expect(api.getEvent).not.toHaveBeenCalled();
    });

    it('records the execution with the zone of the activity and shows the result', async () => {
      event = detailOf({ status: 'scheduled', ...PAST });
      actions.complete.mockReturnValue(
        of(
          versionedOf({
            status: 'completed',
            completion: {
              result: 'succeeded',
              executedAt: '2026-10-24T14:00:00Z',
              completedAt: '2026-10-24T16:00:00Z',
              completedBy: 'Ana Pérez',
              notes: 'Sin novedades',
              evidenceLinks: [],
            },
          }),
        ),
      );
      const { fixture, element } = await render();

      find(element, 'Registrar ejecución')!.click();
      await settle(fixture);

      expect(actions.complete).toHaveBeenCalledWith(
        expect.objectContaining({ id: 'e1' }),
        'America/Bogota',
        null,
      );
      expect(fact(card(element, 'Ejecución'), 'Resultado')).toBe('Realizada con éxito');
    });

    it('goes back to the agenda after deleting a draft', async () => {
      event = detailOf({ status: 'draft' });
      const { fixture, element } = await render();

      find(element, 'Eliminar borrador')!.click();
      await settle(fixture);

      expect(actions.deleteDraft).toHaveBeenCalled();
      expect(navigate).toHaveBeenCalledWith(['/agenda']);
    });

    it('reloads after synchronizing with Outlook', async () => {
      event = detailOf({
        status: 'scheduled',
        outlookEnabled: true,
        outlook: { status: 'error', errorCode: 'graph_timeout', statusAt: null, webLink: null },
      });
      const { fixture, element } = await render();
      api.getEvent.mockClear();

      find(element, 'Sincronizar de nuevo')!.click();
      await settle(fixture);

      expect(actions.sync).toHaveBeenCalled();
      expect(api.getEvent).toHaveBeenCalledTimes(1);
    });
  });

  describe('tags (RF-23, RF-24, CA-25, CA-27)', () => {
    it('shows the tags as buttons that add them to the filter of the agenda (RF-25)', async () => {
      event = detailOf({ tags: ['windows', 'iso-27001'] });
      const { element } = await render();

      const chips = [...card(element, 'Etiquetas')!.querySelectorAll('mat-chip button')];
      expect(chips.map((chip) => text(chip))).toEqual(['windows', 'iso-27001']);
      (chips[0] as HTMLButtonElement).click();

      expect(chips[0].getAttribute('aria-label')).toBe('Filtrar por la etiqueta windows');
      expect(navigate).toHaveBeenCalledWith(['/agenda'], { queryParams: { tags: ['windows'] } });
    });

    it('says there are no tags and offers to add them with agenda.update', async () => {
      const { element } = await render();

      expect(text(card(element, 'Etiquetas'))).toContain('Sin etiquetas');
      expect(find(card(element, 'Etiquetas')!, 'Añadir etiquetas')).toBeTruthy();
    });

    it('does not offer to edit the tags without agenda.update', async () => {
      permissions = ['agenda.read'];
      event = detailOf({ tags: ['windows'] });

      const { element } = await render();

      expect(find(card(element, 'Etiquetas')!, 'Editar etiquetas')).toBeUndefined();
      expect(find(card(element, 'Etiquetas')!, 'Añadir etiquetas')).toBeUndefined();
    });

    it('saves every change at once and shows what the API saved, without touching anything else (RF-24)', async () => {
      event = detailOf({ tags: ['windows'], revision: 4 });
      const { fixture, element } = await render();
      api.getEvent.mockClear();

      find(card(element, 'Etiquetas')!, 'Editar etiquetas')!.click();
      await settle(fixture);
      expect(element.querySelector('app-tags-field')).not.toBeNull();
      api.setTags.mockImplementation(() => of(['windows', 'Producción']));
      fixture.componentInstance['changeTags'](['windows', 'producción']);
      await settle(fixture);

      expect(api.setTags).toHaveBeenCalledWith('e1', ['windows', 'producción']);
      find(card(element, 'Etiquetas')!, 'Listo')!.click();
      await settle(fixture);
      const chips = [...card(element, 'Etiquetas')!.querySelectorAll('mat-chip button')].map((c) =>
        text(c),
      );
      expect(chips).toEqual(['windows', 'Producción']);
      // The tags change neither the version of the activity nor Outlook (CA-27): nothing is reloaded or synchronized.
      expect(api.getEvent).not.toHaveBeenCalled();
      expect(actions.sync).not.toHaveBeenCalled();
    });

    it('lets the tags be edited in any state, also completed and cancelled (RF-24)', async () => {
      for (const status of ['completed', 'cancelled'] as const) {
        event = detailOf({ status, tags: ['windows'], completion: null });
        const { element } = await render();

        expect(find(card(element, 'Etiquetas')!, 'Editar etiquetas'), status).toBeTruthy();
      }
    });

    it('shows the error and goes back to the saved tags when they could not be saved', async () => {
      event = detailOf({ tags: ['windows'] });
      api.setTags.mockReturnValue(
        throwError(() => new ApiError(422, { detail: 'Máximo 10 etiquetas por actividad.' })),
      );
      const { fixture, element } = await render();
      find(card(element, 'Etiquetas')!, 'Editar etiquetas')!.click();
      await settle(fixture);

      fixture.componentInstance['changeTags'](['windows', 'otra']);
      await settle(fixture);

      expect(text(card(element, 'Etiquetas')!.querySelector('p.error'))).toBe(
        'Máximo 10 etiquetas por actividad.',
      );
      expect(fixture.componentInstance['draftTags']()).toEqual(['windows']);
    });
  });

  describe('states of the screen (§8.4)', () => {
    it('shows the loading bar until the activity arrives', async () => {
      const pending = new Subject<{ event: EventDetail; etag: string }>();
      api.getEvent.mockReturnValue(pending);

      const { element } = await render();

      expect(
        element.querySelector('mat-progress-bar[aria-label="Cargando la actividad"]'),
      ).not.toBeNull();
      pending.complete();
    });

    it('says the activity does not exist (404) without offering a retry', async () => {
      api.getEvent.mockReturnValue(throwError(() => new ApiError(404, null)));

      const { element } = await render();

      expect(text(element.querySelector('[role="alert"]'))).toContain(
        'La actividad no existe o ya no está disponible.',
      );
      expect(find(element, 'Reintentar')).toBeUndefined();
      expect(find(element, 'Volver a la agenda')).toBeTruthy();
    });

    it('shows the error with a retry that loads again', async () => {
      api.getEvent.mockReturnValueOnce(throwError(() => new ApiError(500, null)));
      const { fixture, element } = await render();

      expect(text(element.querySelector('[role="alert"]'))).toContain(
        'No se pudo cargar la actividad.',
      );
      find(element, 'Reintentar')!.click();
      await settle(fixture);

      expect(text(element.querySelector('h1'))).toBe('Parches de octubre');
    });
  });
});
