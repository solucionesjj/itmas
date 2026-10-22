import { EventDetail, EventSummary } from './agenda.models';

/**
 * Dates and times of the agenda (SPEC-0006 RNF-9). The API gives local dates and date-times of the zone of the organization
 * (`2026-10-24T09:00:00`, `2026-10-26`) without an offset. They are shown as they come: they never pass through the zone of
 * the browser. Calendar maths below use UTC only as a neutral container for year, month and day.
 */

export interface LocalParts {
  readonly year: number;
  readonly month: number;
  readonly day: number;
  readonly hour: number;
  readonly minute: number;
}

export function parseLocal(value: string): LocalParts {
  const match = /^(\d{4})-(\d{2})-(\d{2})(?:T(\d{2}):(\d{2}))?/.exec(value);
  if (!match) {
    throw new Error(`Not a local date or date-time: ${value}`);
  }
  return {
    year: Number(match[1]),
    month: Number(match[2]),
    day: Number(match[3]),
    hour: Number(match[4] ?? 0),
    minute: Number(match[5] ?? 0),
  };
}

const pad = (value: number): string => String(value).padStart(2, '0');

export const dateOf = (value: string): string => value.slice(0, 10);

export const timeOf = (value: string): string => value.slice(11, 16);

export function toDateString(year: number, month: number, day: number): string {
  return `${String(year).padStart(4, '0')}-${pad(month)}-${pad(day)}`;
}

/** The current date in a time zone, `yyyy-MM-dd`. */
export function todayIn(timeZone: string, now: Date = new Date()): string {
  try {
    return new Intl.DateTimeFormat('en-CA', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
    }).format(now);
  } catch {
    return toDateString(now.getFullYear(), now.getMonth() + 1, now.getDate());
  }
}

/** Day of the week of a local date, as in a calendar that starts on Monday: 0 = Monday … 6 = Sunday. */
export function mondayBasedWeekday(date: string): number {
  const { year, month, day } = parseLocal(date);
  return (new Date(Date.UTC(year, month - 1, day)).getUTCDay() + 6) % 7;
}

export function addDays(date: string, days: number): string {
  const { year, month, day } = parseLocal(date);
  const moved = new Date(Date.UTC(year, month - 1, day + days));
  return toDateString(moved.getUTCFullYear(), moved.getUTCMonth() + 1, moved.getUTCDate());
}

const MONTHS = [
  'enero',
  'febrero',
  'marzo',
  'abril',
  'mayo',
  'junio',
  'julio',
  'agosto',
  'septiembre',
  'octubre',
  'noviembre',
  'diciembre',
];

export const monthName = (month: number): string => MONTHS[month - 1] ?? '';

export const capitalize = (text: string): string => text.charAt(0).toUpperCase() + text.slice(1);

const SHORT_MONTHS = [
  'ene',
  'feb',
  'mar',
  'abr',
  'may',
  'jun',
  'jul',
  'ago',
  'sep',
  'oct',
  'nov',
  'dic',
];
const SHORT_WEEKDAYS = ['lun', 'mar', 'mié', 'jue', 'vie', 'sáb', 'dom'];

/** "31 de diciembre de 2026" */
export function longDate(date: string): string {
  const { year, month, day } = parseLocal(date);
  return `${day} de ${monthName(month)} de ${year}`;
}

/** "sáb 24 oct" */
export function shortDate(date: string): string {
  const { month, day } = parseLocal(date);
  return `${SHORT_WEEKDAYS[mondayBasedWeekday(date)]} ${day} ${SHORT_MONTHS[month - 1]}`;
}

/**
 * When an activity happens, for the lists. A timed one on a single day: "sáb 24 oct · 09:00–11:00"; across days: "sáb 24 oct
 * 22:00 → dom 25 oct 02:00"; all-day: "sáb 24 oct" or "sáb 24 oct → lun 26 oct" (last day inclusive).
 */
export function whenLabel(
  event: Pick<EventSummary | EventDetail, 'start' | 'end' | 'isAllDay'>,
): string {
  const startDate = dateOf(event.start);
  const endDate = dateOf(event.end);
  if (event.isAllDay) {
    return startDate === endDate
      ? `${shortDate(startDate)} · todo el día`
      : `${shortDate(startDate)} → ${shortDate(endDate)} · todo el día`;
  }
  return startDate === endDate
    ? `${shortDate(startDate)} · ${timeOf(event.start)}–${timeOf(event.end)}`
    : `${shortDate(startDate)} ${timeOf(event.start)} → ${shortDate(endDate)} ${timeOf(event.end)}`;
}

/** A UTC instant (creation date, execution…) in the zone of the organization: "24 oct 2026, 09:00". */
export function instantLabel(value: string | null | undefined, timeZone: string): string {
  if (!value) {
    return '';
  }
  try {
    return new Intl.DateTimeFormat('es', {
      timeZone,
      day: 'numeric',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    }).format(new Date(value));
  } catch {
    return value;
  }
}

/** A `datetime-local` style value for an instant, expressed in the zone of the organization (`yyyy-MM-ddTHH:mm`). */
export function instantToLocalInput(value: string, timeZone: string): string {
  const parts = new Intl.DateTimeFormat('en-CA', {
    timeZone,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(new Date(value));
  const get = (type: string) => parts.find((part) => part.type === type)?.value ?? '00';
  return `${get('year')}-${get('month')}-${get('day')}T${get('hour')}:${get('minute')}`;
}

/**
 * The UTC instant (ISO) of a local date-time (`yyyy-MM-ddTHH:mm`) of a time zone. The offset of the zone is looked up for that
 * moment, so it also holds across a daylight-saving change.
 */
export function localToInstant(local: string, timeZone: string): string {
  const { year, month, day, hour, minute } = parseLocal(local);
  const wall = Date.UTC(year, month - 1, day, hour, minute);
  const offsetAt = (instant: number): number => {
    const parts = new Intl.DateTimeFormat('en-CA', {
      timeZone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hourCycle: 'h23',
    }).formatToParts(new Date(instant));
    const get = (type: string) => Number(parts.find((part) => part.type === type)?.value ?? 0);
    return (
      Date.UTC(
        get('year'),
        get('month') - 1,
        get('day'),
        get('hour'),
        get('minute'),
        get('second'),
      ) - instant
    );
  };
  const first = wall - offsetAt(wall);
  const refined = wall - offsetAt(first);
  return new Date(refined).toISOString();
}

/** Date-time (naive, `yyyy-MM-ddTHH:mm`) from a form date and a `HH:mm` time. */
export function joinLocal(date: string, time: string): string {
  return `${date}T${time}`;
}

/**
 * Whether an activity can still be changed as a whole: in Draft or Scheduled and not started yet (RF-5, RF-6). The start is
 * compared as the UTC instant the API gives, so the zone of the browser plays no part.
 */
export function isOpenOccurrence(
  event: Pick<EventDetail | EventSummary, 'status' | 'startUtc'>,
  now: number = Date.now(),
): boolean {
  return (
    (event.status === 'draft' || event.status === 'scheduled') && Date.parse(event.startUtc) > now
  );
}

/** Whether a local date string is a real calendar date. */
export function isRealDate(value: string): boolean {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) {
    return false;
  }
  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
  const date = new Date(Date.UTC(year, month - 1, day));
  return (
    date.getUTCFullYear() === year && date.getUTCMonth() === month - 1 && date.getUTCDate() === day
  );
}
