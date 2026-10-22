import {
  durationLabel,
  outlookErrorText,
  outlookPresentation,
  overduePresentation,
  reminderLabel,
  ruleLabel,
  statusPresentation,
  warningText,
} from './agenda-labels';
import { EventStatus, OutlookStatus, SeriesPattern } from './agenda.models';

describe('states as icon and text (RNF-2)', () => {
  it('gives every event state an icon and a Spanish label', () => {
    const labels = (['draft', 'scheduled', 'completed', 'cancelled'] as EventStatus[]).map(
      (status) => statusPresentation(status),
    );

    expect(labels.map((state) => state.label)).toEqual([
      'Borrador',
      'Programada',
      'Completada',
      'Cancelada',
    ]);
    for (const state of labels) {
      expect(state.icon).toBeTruthy();
    }
  });

  it('draws Vencida as its own mark, with icon and text (RF-8)', () => {
    expect(overduePresentation()).toMatchObject({ label: 'Vencida', icon: 'warning' });
  });

  it('gives every state in Outlook an icon and a label (RF-18)', () => {
    const labels = (['not-published', 'pending', 'published', 'error'] as OutlookStatus[]).map(
      (status) => outlookPresentation(status).label,
    );

    expect(labels).toEqual(['No publicado', 'Pendiente', 'Publicado', 'Error']);
  });
});

describe('outlookErrorText (RF-18)', () => {
  it('has a message for every code of the closed set and never shows the code as text', () => {
    for (const code of [
      'graph_unauthorized',
      'graph_forbidden',
      'graph_not_found',
      'graph_throttled',
      'graph_unavailable',
      'graph_timeout',
      'graph_rejected',
      'not_configured',
    ]) {
      expect(outlookErrorText(code), code).not.toBe('No se pudo publicar en Outlook.');
      expect(outlookErrorText(code), code).not.toContain('graph_');
    }
  });

  it('answers a generic message for an unknown code and nothing without one', () => {
    expect(outlookErrorText('algo_nuevo')).toBe('No se pudo publicar en Outlook.');
    expect(outlookErrorText(null)).toBe('');
  });
});

describe('warningText (§8.4)', () => {
  it('words the freeze overlap and the impact overlap', () => {
    expect(warningText({ kind: 'freeze-overlap', eventId: 'e2', title: 'Cierre contable' })).toBe(
      'Esta actividad se solapa con el congelamiento «Cierre contable».',
    );
    expect(warningText({ kind: 'impact-overlap', eventId: 'e3', title: 'Parches' })).toBe(
      'Se solapa con «Parches», que también tiene impacto.',
    );
  });
});

describe('reminderLabel and durationLabel', () => {
  it('writes a reminder in the largest unit that divides it evenly', () => {
    expect(reminderLabel(0)).toBe('En el momento del inicio');
    expect(reminderLabel(15)).toBe('15 minutos antes');
    expect(reminderLabel(60)).toBe('1 hora antes');
    expect(reminderLabel(120)).toBe('2 horas antes');
    expect(reminderLabel(1440)).toBe('1 día antes');
    expect(reminderLabel(2880)).toBe('2 días antes');
    expect(reminderLabel(45)).toBe('45 minutos antes');
  });

  it('writes a duration the same way', () => {
    expect(durationLabel(1)).toBe('1 minuto');
    expect(durationLabel(90)).toBe('90 minutos');
    expect(durationLabel(60)).toBe('1 hora');
    expect(durationLabel(180)).toBe('3 horas');
    expect(durationLabel(1440)).toBe('1 día');
    expect(durationLabel(2880)).toBe('2 días');
  });
});

describe('ruleLabel (RN-12)', () => {
  const pattern = (overrides: Partial<SeriesPattern>): SeriesPattern => ({
    unit: 'month',
    interval: 1,
    anchor: null,
    startsOn: '2026-11-10',
    endsOn: null,
    count: null,
    ...overrides,
  });

  it('describes "every month, the second Tuesday, until a date" (§8.1)', () => {
    expect(
      ruleLabel(
        pattern({
          anchor: { kind: 'nth-weekday', dayOfMonth: null, nth: 2, weekday: 'tuesday' },
          endsOn: '2026-12-31',
        }),
      ),
    ).toBe('Cada mes, el segundo martes, hasta el 31 de diciembre de 2026');
  });

  it('describes "every 90 days" with its count', () => {
    expect(ruleLabel(pattern({ unit: 'day', interval: 90, count: 4 }))).toBe(
      'Cada 90 días, 4 ocurrencias',
    );
  });

  it('describes a quarterly day of the month, and a yearly one with its month', () => {
    expect(
      ruleLabel(
        pattern({
          interval: 3,
          anchor: { kind: 'day-of-month', dayOfMonth: 31, nth: null, weekday: null },
          count: 8,
        }),
      ),
    ).toBe('Cada 3 meses, el día 31, 8 ocurrencias');
    expect(
      ruleLabel(
        pattern({
          unit: 'year',
          startsOn: '2026-10-15',
          anchor: { kind: 'day-of-month', dayOfMonth: 15, nth: null, weekday: null },
          count: 3,
        }),
      ),
    ).toBe('Cada año, el día 15 de octubre, 3 ocurrencias');
  });

  it('uses the singular for one occurrence and for one week', () => {
    expect(ruleLabel(pattern({ unit: 'week', count: 1 }))).toBe('Cada semana, 1 ocurrencia');
  });
});
