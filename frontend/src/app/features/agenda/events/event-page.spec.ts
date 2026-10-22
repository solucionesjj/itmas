import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { NotificationService } from '../../../core/services/notification-service';
import { AgendaApi } from '../data/agenda-api';
import { provideAgendaDates } from '../data/agenda-date-adapter';
import { FREEZE_TYPE, PATCHING_TYPE, TYPES, versionedOf } from '../data/agenda-fixtures';
import { EventType, PersonOption } from '../data/agenda.models';
import { EventPage } from './event-page';

const ANA: PersonOption = { id: 'u1', name: 'Ana Pérez', isMe: true };
const LUIS: PersonOption = { id: 'u2', name: 'Luis Gómez', isMe: false };

describe('EventPage (RF-3, RF-5, RF-6, §8.4)', () => {
  let types: EventType[];
  let confirmed: boolean;
  const api = {
    listTypes: vi.fn(),
    getEvent: vi.fn(),
    createEvent: vi.fn(),
    updateEvent: vi.fn(),
    updateSeries: vi.fn(),
    listPeople: vi.fn(),
    listTags: vi.fn(),
  };
  const notifications = { info: vi.fn(), error: vi.fn() };
  const dialog = {
    open: vi.fn(() => ({ afterClosed: () => of(confirmed) })),
  };
  let navigate: ReturnType<typeof vi.spyOn>;
  let permissions: string[];

  beforeEach(() => {
    types = [...TYPES];
    confirmed = true;
    permissions = ['agenda.read', 'agenda.create', 'agenda.update', 'agenda.publish'];
    api.listTypes.mockReset().mockImplementation(() => of(types));
    api.getEvent.mockReset().mockImplementation(() => of(versionedOf({ revision: 3 })));
    api.createEvent.mockReset().mockImplementation(() => of(versionedOf({ id: 'new-1' })));
    api.updateEvent.mockReset().mockImplementation(() => of(versionedOf()));
    api.updateSeries.mockReset().mockImplementation(() => of({ updated: 5 }));
    api.listPeople.mockReset().mockReturnValue(of([ANA, LUIS]));
    api.listTags.mockReset().mockReturnValue(of([]));
    notifications.info.mockReset();
    notifications.error.mockReset();
    dialog.open.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAgendaDates(),
        { provide: AgendaApi, useValue: api },
        { provide: NotificationService, useValue: notifications },
        { provide: MatDialog, useValue: dialog },
        {
          provide: AuthService,
          useValue: { hasPermission: (permission: string) => permissions.includes(permission) },
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

  const render = async (inputs: Record<string, string> = {}) => {
    const fixture = TestBed.createComponent(EventPage);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    await settle(fixture);
    const element = fixture.nativeElement as HTMLElement;
    const form = fixture.componentInstance['form'];
    return { fixture, element, form };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const button = (element: HTMLElement, label: string) =>
    [...element.querySelectorAll<HTMLElement>('button, a')].find((b) => text(b).includes(label));
  const labels = (element: HTMLElement) =>
    [...element.querySelectorAll('mat-label')].map((label) => text(label));

  describe('creating', () => {
    it('suggests the person that is creating the activity as the responsible (P-31)', async () => {
      const { element, form } = await render();

      expect(text(element.querySelector('h1'))).toBe('Nueva actividad');
      expect(form.controls.responsible.value).toEqual(ANA);
    });

    it('offers only the active types', async () => {
      types = [PATCHING_TYPE, { ...FREEZE_TYPE, isActive: false }];

      const { fixture } = await render();

      expect(
        fixture.componentInstance['selectableTypes']().map((type: EventType) => type.id),
      ).toEqual([PATCHING_TYPE.id]);
    });

    it('creates the activity with the data of the form and goes to its detail', async () => {
      const { fixture, element, form } = await render();
      form.patchValue({
        typeId: PATCHING_TYPE.id,
        title: 'Parches de noviembre',
        startDate: new Date(2026, 10, 10),
        startTime: '22:00',
        tags: ['windows'],
      });

      button(element, 'Crear actividad')!.click();
      await settle(fixture);

      expect(api.createEvent).toHaveBeenCalledWith(
        expect.objectContaining({
          typeId: PATCHING_TYPE.id,
          title: 'Parches de noviembre',
          responsibleUserId: 'u1',
          isAllDay: false,
          start: '2026-11-10T22:00',
          end: null,
          tags: ['windows'],
        }),
      );
      expect(notifications.info).toHaveBeenCalledWith('Actividad creada en borrador.');
      expect(navigate).toHaveBeenCalledWith(['/agenda/events', 'new-1']);
    });

    it('does not save when a chip is removed with its button, which sits inside the form', async () => {
      const { fixture, element, form } = await render();
      form.patchValue({
        typeId: PATCHING_TYPE.id,
        title: 'Parches',
        startDate: new Date(2026, 10, 10),
        tags: ['windows', 'producción'],
      });
      await settle(fixture);

      element
        .querySelector<HTMLButtonElement>('button[aria-label="Quitar etiqueta windows"]')!
        .click();
      await settle(fixture);

      expect(form.controls.tags.value).toEqual(['producción']);
      expect(api.createEvent).not.toHaveBeenCalled();
    });

    it('does not call the API with an incomplete form and says what to review', async () => {
      const { fixture, element } = await render();

      button(element, 'Crear actividad')!.click();
      await settle(fixture);

      expect(api.createEvent).not.toHaveBeenCalled();
      expect(text(element.querySelector('p.error'))).toBe('Revisa los campos marcados.');
      expect(text(element)).toContain('El título es obligatorio.');
      expect(text(element)).toContain('El tipo es obligatorio.');
    });

    it('shows a business-rule error (422) next to the form and stays on it', async () => {
      api.createEvent.mockReturnValue(
        throwError(() => new ApiError(422, { detail: 'El inicio no puede estar en el pasado.' })),
      );
      const { fixture, element, form } = await render();
      form.patchValue({
        typeId: PATCHING_TYPE.id,
        title: 'Parches',
        startDate: new Date(2020, 0, 1),
      });

      button(element, 'Crear actividad')!.click();
      await settle(fixture);

      expect(text(element.querySelector('p.error'))).toBe('El inicio no puede estar en el pasado.');
      expect(navigate).not.toHaveBeenCalled();
      expect(button(element, 'Crear actividad')).toBeTruthy();
    });

    it('starts on the day and hour of an empty slot of a calendar (RF-12b)', async () => {
      const { form } = await render({ date: '2026-10-24', time: '14:30' });

      expect(form.controls.startDate.value).toEqual(new Date(2026, 9, 24));
      expect(form.controls.startTime.value).toBe('14:30');
    });

    it('ignores a day or an hour that is not valid', async () => {
      const { form } = await render({ date: '2026-02-30', time: '25:00' });

      expect(form.controls.startDate.value).toBeNull();
      expect(form.controls.startTime.value).toBe('09:00');
    });

    it('warns not to write secrets in the description (§8.4)', async () => {
      const { element } = await render();

      expect(text(element)).toContain(
        'No escribas contraseñas, claves ni hallazgos de seguridad: el calendario llega a Outlook.',
      );
    });

    it('turns the dates into days when the activity is all-day (P-24)', async () => {
      const { fixture, element, form } = await render();

      expect(labels(element)).toContain('Hora de inicio');
      form.controls.isAllDay.setValue(true);
      await settle(fixture);

      expect(labels(element)).toContain('Primer día');
      expect(labels(element)).toContain('Último día (inclusive)');
      expect(labels(element)).not.toContain('Hora de inicio');
    });

    it('shows the defaults of the chosen type, and that a freeze period blocks changes', async () => {
      const { fixture, element, form } = await render();

      form.controls.typeId.setValue(FREEZE_TYPE.id);
      await settle(fixture);

      expect(text(element)).toContain('Por defecto: duración de 1 día, recordatorio 1 día antes');
      expect(text(element)).toContain('Periodo de congelamiento');
    });

    it('shows the error of loading with a retry', async () => {
      api.listTypes.mockReturnValueOnce(throwError(() => new ApiError(500, null)));
      const { fixture, element } = await render();

      expect(text(element.querySelector('[role="alert"]'))).toContain(
        'No se pudieron cargar los tipos de actividad.',
      );
      button(element, 'Reintentar')!.click();
      await settle(fixture);

      expect(element.querySelector('app-event-fields')).not.toBeNull();
    });
  });

  describe('editing (RF-5)', () => {
    it('says the activity could not be loaded when editing fails to load it', async () => {
      api.getEvent.mockReturnValueOnce(throwError(() => new ApiError(500, null)));

      const { element } = await render({ id: 'e1' });

      expect(text(element.querySelector('[role="alert"]'))).toContain(
        'No se pudo cargar la actividad.',
      );
    });

    it('loads the activity into the form and keeps its inactive type selectable', async () => {
      types = [PATCHING_TYPE, { ...FREEZE_TYPE, isActive: false }];
      api.getEvent.mockReturnValue(
        of(versionedOf({ type: { id: FREEZE_TYPE.id, name: FREEZE_TYPE.name, isFreeze: true } })),
      );

      const { fixture, element, form } = await render({ id: 'e1' });

      expect(text(element.querySelector('h1'))).toBe('Editar actividad');
      expect(form.controls.title.value).toBe('Parches de octubre');
      expect(
        fixture.componentInstance['selectableTypes']().map((type: EventType) => type.id),
      ).toEqual([PATCHING_TYPE.id, FREEZE_TYPE.id]);
    });

    it('saves with the ETag it read and goes back to the detail', async () => {
      const { fixture, element, form } = await render({ id: 'e1' });
      form.patchValue({ title: 'Parches de octubre (ajustado)' });

      button(element, 'Guardar cambios')!.click();
      await settle(fixture);

      expect(api.updateEvent).toHaveBeenCalledWith(
        'e1',
        '"3"',
        expect.objectContaining({
          title: 'Parches de octubre (ajustado)',
          start: '2026-10-24T09:00',
        }),
      );
      expect(notifications.info).toHaveBeenCalledWith('Actividad actualizada.');
      expect(navigate).toHaveBeenCalledWith(['/agenda/events', 'e1']);
    });

    it('says somebody else changed it when the version is out of date (412) and offers to reload', async () => {
      api.updateEvent.mockReturnValue(throwError(() => new ApiError(412, null)));
      const { fixture, element } = await render({ id: 'e1' });

      button(element, 'Guardar cambios')!.click();
      await settle(fixture);

      expect(text(element.querySelector('p.error'))).toBe(
        'Otro usuario modificó esta actividad. Recarga los datos e inténtalo de nuevo.',
      );
      api.getEvent.mockClear();
      button(element, 'Recargar datos')!.click();
      await settle(fixture);
      expect(api.getEvent).toHaveBeenCalledTimes(1);
      expect(element.querySelector('p.error')).toBeNull();
    });

    it.each([
      ['completed', 'completada'],
      ['cancelled', 'cancelada'],
    ] as const)('does not offer the form for a %s activity (RN-9)', async (status, word) => {
      api.getEvent.mockReturnValue(of(versionedOf({ status })));

      const { element } = await render({ id: 'e1' });

      expect(element.querySelector('app-event-fields')).toBeNull();
      expect(text(element)).toContain(`Una actividad ${word} no se puede modificar.`);
      expect(button(element, 'Volver al detalle')).toBeTruthy();
    });

    it('tells that the changes of a scheduled activity reach Outlook', async () => {
      api.getEvent.mockReturnValue(of(versionedOf({ status: 'scheduled', outlookEnabled: true })));

      const { element } = await render({ id: 'e1' });

      expect(text(element)).toContain('los cambios se reflejarán en Outlook');
    });

    it('does not talk about Outlook when it is not configured', async () => {
      api.getEvent.mockReturnValue(of(versionedOf({ status: 'scheduled', outlookEnabled: false })));

      const { element } = await render({ id: 'e1' });

      expect(text(element)).not.toContain('se reflejarán en Outlook');
    });

    it('P-12: does not offer the form of a scheduled activity to whom cannot publish, but a draft yes', async () => {
      permissions = ['agenda.read', 'agenda.update'];
      api.getEvent.mockReturnValue(of(versionedOf({ status: 'scheduled' })));

      const { element } = await render({ id: 'e1' });

      expect(element.querySelector('app-event-fields')).toBeNull();
      expect(text(element)).toContain('Para modificarla necesitas también el permiso');
      expect(button(element, 'Volver al detalle')).toBeTruthy();

      api.getEvent.mockReturnValue(of(versionedOf({ status: 'draft' })));
      const draft = await render({ id: 'e2' });
      expect(draft.element.querySelector('app-event-fields')).not.toBeNull();
    });
  });

  describe('the common fields of a series (RF-6, P-29)', () => {
    const farFuture = {
      seriesId: 's1',
      startUtc: '2099-01-10T14:00:00Z',
      endUtc: '2099-01-10T15:00:00Z',
      start: '2099-01-10T09:00:00',
      end: '2099-01-10T10:00:00',
    };

    it('offers the series edit from an open occurrence', async () => {
      api.getEvent.mockReturnValue(of(versionedOf(farFuture)));

      const { element } = await render({ id: 'e1' });

      expect(text(element)).toContain('Esta actividad forma parte de una serie.');
      expect(button(element, 'Editar los campos comunes de la serie')).toBeTruthy();
    });

    it('does not offer it from an occurrence that already started', async () => {
      api.getEvent.mockReturnValue(
        of(versionedOf({ ...farFuture, startUtc: '2020-01-10T14:00:00Z', status: 'scheduled' })),
      );

      const { element } = await render({ id: 'e1', scope: 'series' });

      expect(text(element.querySelector('h1'))).toBe('Editar actividad');
      expect(element.querySelector('app-event-fields')).not.toBeNull();
      expect(text(element)).not.toContain('Editar los campos comunes');
    });

    it('edits the common fields only: no dates and no notice option', async () => {
      api.getEvent.mockReturnValue(of(versionedOf(farFuture)));

      const { element } = await render({ id: 'e1', scope: 'series' });

      expect(text(element.querySelector('h1'))).toBe('Editar la serie');
      expect(labels(element)).not.toContain('Fecha de inicio');
      expect(labels(element)).not.toContain('Hora de inicio');
      expect(text(element)).not.toContain('Notificar al equipo');
      expect(labels(element)).toContain('Título');
      expect(labels(element)).toContain('Responsable');
    });

    it('asks for the confirmation of §8.4 and applies the change to the series, not to the activity', async () => {
      api.getEvent.mockReturnValue(of(versionedOf(farFuture)));
      const { fixture, element, form } = await render({ id: 'e1', scope: 'series' });
      form.patchValue({ title: 'Parches mensuales', tags: ['iso-27001'], responsible: LUIS });

      button(element, 'Aplicar a la serie')!.click();
      await settle(fixture);

      expect(dialog.open).toHaveBeenCalledWith(
        expect.anything(),
        expect.objectContaining({
          data: expect.objectContaining({
            message:
              'El cambio se aplicará a las ocurrencias de la serie que aún no han empezado. Las fechas no cambian.',
          }),
        }),
      );
      expect(api.updateSeries).toHaveBeenCalledWith(
        's1',
        expect.objectContaining({
          title: 'Parches mensuales',
          responsibleUserId: 'u2',
          tags: ['iso-27001'],
        }),
      );
      expect(api.updateSeries.mock.calls[0][1]).not.toHaveProperty('start');
      expect(api.updateEvent).not.toHaveBeenCalled();
      expect(notifications.info).toHaveBeenCalledWith('Se actualizaron 5 actividades de la serie.');
      expect(navigate).toHaveBeenCalledWith(['/agenda/events', 'e1']);
    });

    it('does nothing when the person does not confirm', async () => {
      confirmed = false;
      api.getEvent.mockReturnValue(of(versionedOf(farFuture)));
      const { fixture, element } = await render({ id: 'e1', scope: 'series' });

      button(element, 'Aplicar a la serie')!.click();
      await settle(fixture);

      expect(api.updateSeries).not.toHaveBeenCalled();
    });
  });
});
