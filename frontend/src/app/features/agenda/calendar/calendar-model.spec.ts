import { summaryOf } from '../data/agenda-fixtures';
import { AgendaState } from '../data/agenda-state';
import {
  CalendarEventProps,
  agendaView,
  anchorDate,
  fullCalendarView,
  toCalendarEvent,
} from './calendar-model';

const state = (overrides: Partial<AgendaState>): AgendaState => ({
  view: 'list',
  year: 2026,
  month: null,
  date: null,
  filters: { type: '', status: '', responsible: '', mine: false, tags: [], search: '' },
  ...overrides,
});

describe('the views of the calendar (RF-12, §10.4)', () => {
  it('maps our views to the ones of FullCalendar: year, month and week', () => {
    expect(fullCalendarView('year', false)).toBe('multiMonthYear');
    expect(fullCalendarView('month', false)).toBe('dayGridMonth');
    expect(fullCalendarView('week', false)).toBe('timeGridWeek');
  });

  it('shows the week as one day at a time in a narrow screen', () => {
    expect(fullCalendarView('week', true)).toBe('timeGridDay');
    expect(fullCalendarView('month', true)).toBe('dayGridMonth');
    expect(fullCalendarView('year', true)).toBe('multiMonthYear');
  });

  it('reads FullCalendar views back as ours, and ignores the ones it does not offer', () => {
    expect(agendaView('multiMonthYear')).toBe('year');
    expect(agendaView('dayGridMonth')).toBe('month');
    for (const type of ['timeGridWeek', 'timeGridDay', 'dayGridWeek', 'dayGridDay']) {
      expect(agendaView(type)).toBe('week');
    }
    expect(agendaView('listYear')).toBeNull();
  });

  it('opens on January 1st for the year, the 1st for the month and the day itself for the week', () => {
    expect(anchorDate(state({ view: 'year', year: 2027 }))).toBe('2027-01-01');
    expect(anchorDate(state({ view: 'month', year: 2026, month: 3 }))).toBe('2026-03-01');
    expect(anchorDate(state({ view: 'week', year: 2026, date: '2026-10-21' }))).toBe('2026-10-21');
  });
});

describe('an activity as an event of the calendar (CA-32, CA-33)', () => {
  const props = (event = summaryOf()): CalendarEventProps =>
    toCalendarEvent(event).extendedProps as CalendarEventProps;

  it('keeps the local date-times as they come, with no offset, so the zone of the browser plays no part (RNF-9)', () => {
    const input = toCalendarEvent(
      summaryOf({ start: '2026-10-24T09:00:00', end: '2026-10-24T11:00:00' }),
    );

    expect(input.start).toBe('2026-10-24T09:00:00');
    expect(input.end).toBe('2026-10-24T11:00:00');
    expect(input.allDay).toBe(false);
  });

  it('keeps an activity that crosses midnight as one event: the calendar splits it between the two days', () => {
    const input = toCalendarEvent(
      summaryOf({ start: '2026-10-24T22:00:00', end: '2026-10-25T02:00:00' }),
    );

    expect([input.start, input.end]).toEqual(['2026-10-24T22:00:00', '2026-10-25T02:00:00']);
  });

  it('ends an all-day activity on the day after its last day, because the calendar wants an exclusive end (P-24)', () => {
    const one = toCalendarEvent(
      summaryOf({ isAllDay: true, start: '2026-10-26', end: '2026-10-26' }),
    );
    const several = toCalendarEvent(
      summaryOf({ isAllDay: true, start: '2026-12-30', end: '2027-01-02' }),
    );

    expect([one.start, one.end, one.allDay]).toEqual(['2026-10-26', '2026-10-27', true]);
    expect([several.start, several.end]).toEqual(['2026-12-30', '2027-01-03']);
  });

  it('links to the detail with the id alone and carries the id', () => {
    const input = toCalendarEvent(summaryOf({ id: 'a b/c' }));

    expect(input.id).toBe('a b/c');
    expect(input.url).toBe('/agenda/events/a%20b%2Fc');
  });

  it('gives each state its class, its icon and its text: the colour is never the only signal (RNF-10)', () => {
    const states = (['draft', 'scheduled', 'completed', 'cancelled'] as const).map((status) => {
      const input = toCalendarEvent(summaryOf({ status }));
      return [
        input.className,
        props(summaryOf({ status })).statusIcon,
        props(summaryOf({ status })).statusLabel,
      ];
    });

    expect(states).toEqual([
      ['agenda-event agenda-event--draft', 'edit_note', 'Borrador'],
      ['agenda-event agenda-event--scheduled', 'event_available', 'Programada'],
      ['agenda-event agenda-event--completed', 'check_circle', 'Completada'],
      ['agenda-event agenda-event--cancelled', 'cancel', 'Cancelada'],
    ]);
  });

  it('marks an overdue activity next to its state, not instead of it (RF-8)', () => {
    const overdue = summaryOf({ status: 'scheduled', isOverdue: true });

    expect(toCalendarEvent(overdue).className).toBe(
      'agenda-event agenda-event--scheduled agenda-event--overdue',
    );
    expect(props(overdue)).toMatchObject({
      overdue: true,
      statusIcon: 'warning',
      statusLabel: 'Programada, Vencida',
    });
  });

  it('takes the colours from roles of the Material theme, not from colours of its own (§6.3)', () => {
    for (const status of ['draft', 'scheduled', 'completed', 'cancelled'] as const) {
      const input = toCalendarEvent(summaryOf({ status }));

      expect(input.color).toMatch(/^var\(--mat-sys-[a-z-]+\)$/);
      expect(input.contrastColor).toMatch(/^var\(--mat-sys-[a-z-]+\)$/);
    }
    expect(toCalendarEvent(summaryOf({ isOverdue: true })).color).toBe(
      'var(--mat-sys-error-container)',
    );
  });

  it('gives the accessible name of the event: title, type, state and when', () => {
    expect(props(summaryOf({ status: 'scheduled' })).label).toBe(
      'Parches de octubre, Mantenimiento y parches de sistema operativo, Programada, sáb 24 oct · 09:00–11:00',
    );
    expect(
      props(
        summaryOf({
          status: 'draft',
          isOverdue: true,
          isAllDay: true,
          start: '2026-10-26',
          end: '2026-10-26',
        }),
      ).label,
    ).toBe(
      'Parches de octubre, Mantenimiento y parches de sistema operativo, Borrador, Vencida, lun 26 oct · todo el día',
    );
  });

  it('passes what a person typed as plain text: nothing is turned into markup', () => {
    const input = toCalendarEvent(summaryOf({ title: '<img src=x onerror=alert(1)>' }));

    expect(input.title).toBe('<img src=x onerror=alert(1)>');
    expect(JSON.stringify(input.extendedProps)).not.toContain('<b>');
  });
});
