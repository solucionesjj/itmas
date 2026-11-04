import type { EventInput } from '@fullcalendar/angular';
import type { StatusTone } from '../../../shared/models/status-tone';
import { statusPresentation, overduePresentation } from '../data/agenda-labels';
import { AgendaState, CalendarView } from '../data/agenda-state';
import { addDays, dateOf, toDateString, whenLabel } from '../data/agenda-time';
import { EventStatus, EventSummary } from '../data/agenda.models';

/**
 * What the calendar views need from the agenda (SPEC-0006 §10.4, RF-12): the mapping between our views and the ones of
 * FullCalendar, and the activities as events of the calendar. Pure functions, with no FullCalendar runtime: the dates are local
 * to the organization zone and travel as the API gives them (RNF-9).
 */

/** The view and the day FullCalendar is on, as our state knows them. */
export interface CalendarPosition {
  readonly view: CalendarView;
  readonly date: string;
}

/** An empty day (or hour) chosen to plan an activity there (RF-12b, P-21). */
export interface CalendarSlot {
  readonly date: string;
  /** `HH:mm`, or null for a whole day (the month view). */
  readonly time: string | null;
}

export type FullCalendarView = 'multiMonthYear' | 'dayGridMonth' | 'timeGridWeek' | 'timeGridDay';

/** The view of FullCalendar for ours. A narrow screen shows the week as one day at a time (§10.4). */
export function fullCalendarView(view: CalendarView, narrow: boolean): FullCalendarView {
  switch (view) {
    case 'year':
      return 'multiMonthYear';
    case 'month':
      return 'dayGridMonth';
    default:
      return narrow ? 'timeGridDay' : 'timeGridWeek';
  }
}

/** Our view for one of FullCalendar (it changes view by itself when a month title or a day is chosen), or null if it is not ours. */
export function agendaView(type: string): CalendarView | null {
  switch (type) {
    case 'multiMonthYear':
      return 'year';
    case 'dayGridMonth':
      return 'month';
    case 'timeGridWeek':
    case 'timeGridDay':
    case 'dayGridWeek':
    case 'dayGridDay':
      return 'week';
    default:
      return null;
  }
}

/** The date FullCalendar opens on for a state: January 1st, the 1st of the month or the day of the week view. */
export function anchorDate(state: Pick<AgendaState, 'view' | 'year' | 'month' | 'date'>): string {
  switch (state.view) {
    case 'month':
      return toDateString(state.year, state.month ?? 1, 1);
    case 'week':
      return state.date ?? toDateString(state.year, 1, 1);
    default:
      return toDateString(state.year, 1, 1);
  }
}

/**
 * The colours of a state: the containers of the activities take the tones of the states (SPEC-0010 RF-8, ADR-0017 note), tokens of
 * the theme layer, so the light and dark schemes keep working (§6.3). The colour reinforces the meaning; the state is always also
 * written (icon and text, RNF-10).
 */
const STATE_TONES: Record<EventStatus | 'overdue', StatusTone> = {
  draft: 'neutral',
  scheduled: 'info',
  completed: 'positive',
  cancelled: 'neutral',
  overdue: 'risk',
};

/** Background and text colour of an activity of the given state, as `--itmas-tone-*` tokens. */
export function stateColors(state: EventStatus | 'overdue'): {
  color: string;
  contrastColor: string;
} {
  // An unknown state (a newer API) is neutral, like the inventory states.
  const tone = STATE_TONES[state] ?? 'neutral';
  return { color: `var(--itmas-tone-${tone}-bg)`, contrastColor: `var(--itmas-tone-${tone})` };
}

/** What the template of an event of the calendar needs, kept in `extendedProps`. */
export interface CalendarEventProps {
  readonly status: EventStatus;
  readonly statusIcon: string;
  readonly statusLabel: string;
  readonly overdue: boolean;
  readonly typeName: string;
  /** "Título, tipo, estado, fecha": the accessible name of the event. */
  readonly label: string;
}

/**
 * An activity as an event of the calendar. The date-times are local and carry no offset: with the zone of the organization set
 * in the calendar they are drawn as they are, never through the zone of the browser. An all-day activity ends on its last day
 * (inclusive, P-24) and FullCalendar wants the day after (exclusive). Everything a person typed travels as text; the link is built
 * from the id alone.
 */
export function toCalendarEvent(event: EventSummary): EventInput {
  const status = statusPresentation(event.status);
  const colors = stateColors(event.isOverdue ? 'overdue' : event.status);
  const props: CalendarEventProps = {
    status: event.status,
    statusIcon: event.isOverdue ? overduePresentation().icon : status.icon,
    statusLabel: event.isOverdue ? `${status.label}, ${overduePresentation().label}` : status.label,
    overdue: event.isOverdue,
    typeName: event.type.name,
    label: `${event.title}, ${event.type.name}, ${status.label}${event.isOverdue ? ', ' + overduePresentation().label : ''}, ${whenLabel(event)}`,
  };
  return {
    id: event.id,
    title: event.title,
    start: event.isAllDay ? dateOf(event.start) : event.start,
    end: event.isAllDay ? addDays(dateOf(event.end), 1) : event.end,
    allDay: event.isAllDay,
    url: `/agenda/events/${encodeURIComponent(event.id)}`,
    className: [
      'agenda-event',
      `agenda-event--${event.status}`,
      event.isOverdue ? 'agenda-event--overdue' : '',
    ]
      .filter(Boolean)
      .join(' '),
    color: colors.color,
    contrastColor: colors.contrastColor,
    extendedProps: props,
  };
}

export const toCalendarEvents = (events: readonly EventSummary[]): EventInput[] =>
  events.map(toCalendarEvent);
