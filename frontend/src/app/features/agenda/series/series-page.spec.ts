import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { NotificationService } from '../../../core/services/notification-service';
import { AgendaApi } from '../data/agenda-api';
import { provideAgendaDates } from '../data/agenda-date-adapter';
import { PATCHING_TYPE, TYPES } from '../data/agenda-fixtures';
import { PersonOption, SeriesOccurrence } from '../data/agenda.models';
import { SeriesPage } from './series-page';

const ANA: PersonOption = { id: 'u1', name: 'Ana Pérez', isMe: true };

const occurrence = (
  start: string,
  warnings: SeriesOccurrence['warnings'] = [],
): SeriesOccurrence => ({
  start,
  end: start.replace('09:00', '10:00'),
  isAllDay: false,
  warnings,
});

describe('SeriesPage (RF-4, CA-3, §8.1)', () => {
  let preview: SeriesOccurrence[];
  const api = {
    listTypes: vi.fn(),
    previewSeries: vi.fn(),
    createSeries: vi.fn(),
    listPeople: vi.fn(),
    listTags: vi.fn(),
  };
  const notifications = { info: vi.fn(), error: vi.fn() };
  let navigate: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    preview = [
      occurrence('2026-11-10T09:00:00'),
      occurrence('2026-12-08T09:00:00'),
      occurrence('2027-01-12T09:00:00'),
    ];
    api.listTypes.mockReset().mockReturnValue(of(TYPES));
    api.previewSeries.mockReset().mockImplementation(() => of(preview));
    api.createSeries
      .mockReset()
      .mockImplementation(() => of({ seriesId: 's1', eventIds: ['a', 'b', 'c'] }));
    api.listPeople.mockReset().mockReturnValue(of([ANA]));
    api.listTags.mockReset().mockReturnValue(of([]));
    notifications.info.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideAgendaDates(),
        { provide: AgendaApi, useValue: api },
        { provide: NotificationService, useValue: notifications },
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
    const fixture = TestBed.createComponent(SeriesPage);
    await settle(fixture);
    return {
      fixture,
      element: fixture.nativeElement as HTMLElement,
      event: fixture.componentInstance['eventForm'],
      rule: fixture.componentInstance['rule'],
    };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const button = (element: HTMLElement, label: string) =>
    [...element.querySelectorAll<HTMLButtonElement>('button')].find((b) =>
      text(b).includes(label),
    )!;

  /** The first activity, filled in: "Parches de SO, every month from 10 November at 09:00". */
  const fill = (event: Awaited<ReturnType<typeof render>>['event']) =>
    event.patchValue({
      typeId: PATCHING_TYPE.id,
      title: 'Parches de SO',
      startDate: new Date(2026, 10, 10),
      startTime: '09:00',
    });

  it('shows the first activity and the repetition, without a preview and with "Crear serie" disabled', async () => {
    const { element } = await render();

    expect(text(element.querySelector('h1'))).toBe('Nueva serie');
    expect(text(element)).toContain('Primera actividad');
    expect(text(element)).toContain('Repetición');
    expect(element.querySelector('table')).toBeNull();
    expect(button(element, 'Crear serie').disabled).toBe(true);
  });

  it('CA-3: the preview shows the dates the series would have and saves nothing', async () => {
    const { fixture, element, event } = await render();
    fill(event);

    button(element, 'Vista previa').click();
    await settle(fixture);

    expect(api.previewSeries).toHaveBeenCalledTimes(1);
    expect(api.createSeries).not.toHaveBeenCalled();
    const rows = [...element.querySelectorAll('table tr.mat-mdc-row')].map((row) =>
      [...row.querySelectorAll('td')].map((cell) => text(cell)),
    );
    expect(rows.map((row) => row[1])).toEqual([
      'mar 10 nov · 09:00–10:00',
      'mar 8 dic · 09:00–10:00',
      'mar 12 ene · 09:00–10:00',
    ]);
    expect(text(element.querySelector('#preview-title + p'))).toContain(
      'Se crearán 3 actividades en borrador.',
    );
  });

  it('sends the first activity and the rule to the preview', async () => {
    const { fixture, element, event, rule } = await render();
    fill(event);
    rule.patchValue({
      unit: 'month',
      interval: 1,
      anchorKind: 'nth-weekday',
      nth: 2,
      weekday: 'tuesday',
      count: 6,
    });

    button(element, 'Vista previa').click();
    await settle(fixture);

    expect(api.previewSeries).toHaveBeenCalledWith({
      event: expect.objectContaining({
        title: 'Parches de SO',
        start: '2026-11-10T09:00',
        isAllDay: false,
      }),
      rule: {
        unit: 'month',
        interval: 1,
        anchor: { kind: 'nth-weekday', nth: 2, weekday: 'tuesday' },
        endsOn: null,
        count: 6,
      },
    });
  });

  it('CA-11: shows the warnings of each date with icon and text, and says they do not prevent creating', async () => {
    preview = [
      occurrence('2026-11-10T09:00:00', [
        { kind: 'freeze-overlap', eventId: 'f1', title: 'Cierre contable' },
      ]),
      occurrence('2026-12-08T09:00:00'),
    ];
    const { fixture, element, event } = await render();
    fill(event);

    button(element, 'Vista previa').click();
    await settle(fixture);

    const warning = element.querySelector('app-warning-list li');
    expect(text(warning?.querySelector('mat-icon'))).toBe('warning');
    expect(text(warning?.querySelector('span'))).toBe(
      'Esta actividad se solapa con el congelamiento «Cierre contable».',
    );
    expect(text(element.querySelector('#preview-title + p'))).toContain(
      '1 tiene advertencias: no impiden crearlas.',
    );
    expect(button(element, 'Crear serie').disabled).toBe(false);
  });

  it('creates the series that was previewed, tells how many activities and goes to the year of the first one', async () => {
    const { fixture, element, event } = await render();
    fill(event);
    button(element, 'Vista previa').click();
    await settle(fixture);
    expect(button(element, 'Crear serie').disabled).toBe(false);

    button(element, 'Crear serie').click();
    await settle(fixture);

    expect(api.createSeries).toHaveBeenCalledTimes(1);
    expect(notifications.info).toHaveBeenCalledWith('Serie creada con 3 actividades en borrador.');
    expect(navigate).toHaveBeenCalledWith(['/agenda'], { queryParams: { year: '2026' } });
  });

  it('invalidates the preview when the data change: what is created is what was shown', async () => {
    const { fixture, element, event, rule } = await render();
    fill(event);
    button(element, 'Vista previa').click();
    await settle(fixture);

    rule.patchValue({ interval: 2 });
    await settle(fixture);

    expect(button(element, 'Crear serie').disabled).toBe(true);
    expect(text(element)).toContain('Cambiaste los datos: vuelve a pedir la vista previa');
    button(element, 'Crear serie').click();
    expect(api.createSeries).not.toHaveBeenCalled();
  });

  it('asks the person to review the fields instead of calling the API when something is missing', async () => {
    const { fixture, element } = await render();

    button(element, 'Vista previa').click();
    await settle(fixture);

    expect(api.previewSeries).not.toHaveBeenCalled();
    expect(text(element.querySelector('p.error'))).toBe('Revisa los campos marcados.');
  });

  it('RN-6: asks for an interval of 1 to 99 and for at most 60 activities', async () => {
    const { fixture, element, event, rule } = await render();
    fill(event);
    rule.patchValue({ interval: 100, count: 61 });

    button(element, 'Vista previa').click();
    await settle(fixture);

    expect(api.previewSeries).not.toHaveBeenCalled();
    expect(text(element)).toContain('Indica un número entero de 1 a 99.');
    expect(text(element)).toContain('Indica un número entero de 1 a 60.');
  });

  it('asks for the final date when the limit is a date', async () => {
    const { fixture, element, event, rule } = await render();
    fill(event);
    rule.patchValue({ limitKind: 'date' });

    button(element, 'Vista previa').click();
    await settle(fixture);

    expect(api.previewSeries).not.toHaveBeenCalled();
    expect(text(element)).toContain('Indica la fecha final.');
  });

  it('shows the anchors only for months and years', async () => {
    const { fixture, element, rule } = await render();
    expect(text(element)).toContain('Qué día se repite');

    rule.patchValue({ unit: 'day', interval: 90 });
    await settle(fixture);

    expect(text(element)).not.toContain('Qué día se repite');
  });

  it('shows a business-rule error (422) of the preview and keeps no preview (RN-16)', async () => {
    api.previewSeries.mockReturnValue(
      throwError(() => new ApiError(422, { detail: 'El inicio no puede estar en el pasado.' })),
    );
    const { fixture, element, event } = await render();
    fill(event);

    button(element, 'Vista previa').click();
    await settle(fixture);

    expect(text(element.querySelector('p.error'))).toBe('El inicio no puede estar en el pasado.');
    expect(element.querySelector('table')).toBeNull();
    expect(button(element, 'Crear serie').disabled).toBe(true);
  });

  it('shows a business-rule error (422) of the creation and stays on the page', async () => {
    api.createSeries.mockReturnValue(
      throwError(
        () => new ApiError(422, { detail: 'Una serie admite como máximo 60 ocurrencias.' }),
      ),
    );
    const { fixture, element, event } = await render();
    fill(event);
    button(element, 'Vista previa').click();
    await settle(fixture);

    button(element, 'Crear serie').click();
    await settle(fixture);

    expect(text(element.querySelector('p.error'))).toBe(
      'Una serie admite como máximo 60 ocurrencias.',
    );
    expect(navigate).not.toHaveBeenCalled();
  });

  it('offers only the active types and a retry when they cannot be loaded', async () => {
    api.listTypes.mockReturnValueOnce(throwError(() => new ApiError(500, null)));
    const { fixture, element } = await render();

    expect(text(element.querySelector('[role="alert"]'))).toContain(
      'No se pudieron cargar los tipos de actividad.',
    );
    button(element, 'Reintentar').click();
    await settle(fixture);

    expect(element.querySelector('app-event-fields')).not.toBeNull();
  });
});
