import { Params } from '@angular/router';
import { AgendaFilters, NO_FILTERS } from './agenda-api';
import { StatusFilter } from './agenda.models';
import {
  addDays,
  capitalize,
  isRealDate,
  mondayBasedWeekday,
  monthName,
  parseLocal,
  shortDate,
  toDateString,
} from './agenda-time';

/**
 * State of the agenda page kept in the address (SPEC-0006 RF-12): the view, the period and the filters, so a page can be
 * saved or shared. Everything has a default and defaults are not written, to keep addresses short.
 */
export type AgendaView = 'list' | 'year' | 'month' | 'week';

/** The four views of RF-12, in the order of the selector. An address asking for another one falls back to the year list. */
export const AVAILABLE_VIEWS: readonly AgendaView[] = ['list', 'year', 'month', 'week'];

export const DEFAULT_VIEW: AgendaView = 'list';

/** The views FullCalendar draws; the year list is a table of Angular Material. */
export type CalendarView = Exclude<AgendaView, 'list'>;

export const isCalendarView = (view: AgendaView): view is CalendarView => view !== 'list';

export interface AgendaState {
  readonly view: AgendaView;
  readonly year: number;
  /**
   * A month (1-12). In the year list it narrows the list; in the month view it is the month shown (never null there); the year
   * view and the week view do not use it.
   */
  readonly month: number | null;
  /** The day the week view is on (`yyyy-MM-dd`; in a narrow screen, the one day shown). Null in the other views. */
  readonly date: string | null;
  readonly filters: AgendaFilters;
}

const STATUSES: readonly StatusFilter[] = [
  'draft',
  'scheduled',
  'completed',
  'cancelled',
  'overdue',
];

/** Minimal view of the query parameters: `ParamMap` implements it. */
export interface ParamReader {
  get(name: string): string | null;
  getAll(name: string): string[];
}

/**
 * Reads the state from the query parameters. A view that is not offered, a year out of range or a value that is not one of the
 * closed sets is ignored. Without an explicit view, tags or a search show the year list: a tag rarely sits in a single month.
 */
export function parseAgendaState(
  params: ParamReader,
  defaultYear: number,
  today: string = toDateString(defaultYear, 1, 1),
): AgendaState {
  const requested = params.get('view') as AgendaView | null;
  const hasContentFilter = params.getAll('tags').length > 0 || !!params.get('search');
  const view =
    requested && AVAILABLE_VIEWS.includes(requested)
      ? requested
      : hasContentFilter
        ? 'list'
        : DEFAULT_VIEW;

  const status = params.get('status') as StatusFilter | null;

  // The week view is anchored on a day, which also gives its year; the others on a year and, for the month view, a month.
  const requestedDate = params.get('date');
  const weekDate =
    view === 'week' ? (requestedDate && isRealDate(requestedDate) ? requestedDate : today) : null;
  const year = weekDate
    ? parseLocal(weekDate).year
    : (integerIn(params.get('year'), 2000, 2200) ?? defaultYear);
  const requestedMonth = integerIn(params.get('month'), 1, 12);
  const month =
    view === 'week' || view === 'year'
      ? null
      : view === 'month'
        ? (requestedMonth ?? defaultMonth(year, today))
        : requestedMonth;

  return {
    view,
    year,
    month,
    date: weekDate,
    filters: {
      type: params.get('type') ?? '',
      status: status && STATUSES.includes(status) ? status : '',
      responsible: params.get('responsible') ?? '',
      mine: params.get('mine') === 'true',
      tags: dedupe(params.getAll('tags')),
      search: params.get('search') ?? '',
    },
  };
}

/** Query parameters of a state (default values omitted); `null` removes a parameter when navigating. */
export function toQueryParams(state: AgendaState, defaultYear: number): Params {
  const { filters } = state;
  return {
    view: state.view === DEFAULT_VIEW ? null : state.view,
    // The week view is told by its day alone.
    year: state.view === 'week' || state.year === defaultYear ? null : state.year,
    month: state.view === 'week' || state.view === 'year' ? null : state.month,
    date: state.view === 'week' ? state.date : null,
    type: filters.type || null,
    status: filters.status || null,
    responsible: filters.responsible || null,
    mine: filters.mine ? 'true' : null,
    tags: filters.tags.length > 0 ? [...filters.tags] : null,
    search: filters.search || null,
  };
}

/** Whether any filter is on (the "Limpiar filtros" button and the empty-state wording depend on it). */
export function hasFilters(filters: AgendaFilters): boolean {
  return (
    filters.type !== '' ||
    filters.status !== '' ||
    filters.responsible !== '' ||
    filters.mine ||
    filters.tags.length > 0 ||
    filters.search !== ''
  );
}

export const clearedFilters = (): AgendaFilters => NO_FILTERS;

/** Local dates `[from, to)` of a year, for `GET /agenda-events` (`to` exclusive). */
export function yearRange(year: number): { from: string; to: string } {
  return {
    from: `${String(year).padStart(4, '0')}-01-01`,
    to: `${String(year + 1).padStart(4, '0')}-01-01`,
  };
}

/** Local dates `[from, to)` of a month, `to` exclusive. */
export function monthRange(year: number, month: number): { from: string; to: string } {
  const pad = (value: number) => String(value).padStart(2, '0');
  const next = month === 12 ? { year: year + 1, month: 1 } : { year, month: month + 1 };
  return {
    from: `${String(year).padStart(4, '0')}-${pad(month)}-01`,
    to: `${String(next.year).padStart(4, '0')}-${pad(next.month)}-01`,
  };
}

/** What an export covers: the week of the week view, the month the view is on or the list is narrowed to, or else the whole year, and the name of the file (RF-21). */
export interface ExportTarget {
  readonly from: string;
  readonly to: string;
  readonly fileName: string;
}

export function exportTarget(
  state: Pick<AgendaState, 'year' | 'month'> & Partial<Pick<AgendaState, 'view' | 'date'>>,
): ExportTarget {
  if (state.view === 'week' && state.date) {
    const { from, to } = weekRange(state.date);
    return { from, to, fileName: `agenda-semana-${from}.ics` };
  }
  const year = String(state.year).padStart(4, '0');
  if (state.month === null) {
    return { ...yearRange(state.year), fileName: `agenda-${year}.ics` };
  }
  return {
    ...monthRange(state.year, state.month),
    fileName: `agenda-${year}-${String(state.month).padStart(2, '0')}.ics`,
  };
}

function integerIn(value: string | null, min: number, max: number): number | null {
  if (value === null || !/^\d+$/.test(value)) {
    return null;
  }
  const parsed = Number(value);
  return parsed >= min && parsed <= max ? parsed : null;
}

function dedupe(values: readonly string[]): string[] {
  const seen = new Set<string>();
  return values.filter((value) => {
    const key = value.toLowerCase();
    if (value === '' || seen.has(key)) {
      return false;
    }
    seen.add(key);
    return true;
  });
}

/** The month a month view opens on when the address does not say: the current one in the current year, January otherwise. */
function defaultMonth(year: number, today: string): number {
  const now = parseLocal(today);
  return now.year === year ? now.month : 1;
}

/** The Monday of the week a local date belongs to. */
export function weekStart(date: string): string {
  return addDays(date, -mondayBasedWeekday(date));
}

/** Local dates `[from, to)` of the week (Monday to Sunday) a date belongs to. */
export function weekRange(date: string): { from: string; to: string } {
  const from = weekStart(date);
  return { from, to: addDays(from, 7) };
}

/**
 * Local dates `[from, to)` of the grid of a month: whole weeks from the Monday on or before the 1st to the Sunday on or after the
 * last day, so the days of the neighbouring months that the grid shows are asked for too.
 */
export function monthGridRange(year: number, month: number): { from: string; to: string } {
  const first = toDateString(year, month, 1);
  const last = addDays(monthRange(year, month).to, -1);
  return { from: weekStart(first), to: addDays(weekStart(last), 7) };
}

/** What the page asks the API for: the year for the list and the year view, and the visible grid for the month and the week. */
export function visibleRange(state: AgendaState): { from: string; to: string } {
  switch (state.view) {
    case 'month':
      return monthGridRange(state.year, state.month ?? 1);
    case 'week':
      return weekRange(state.date ?? toDateString(state.year, 1, 1));
    default:
      return yearRange(state.year);
  }
}

/** "Anterior" (-1) and "Siguiente" (+1): a year, a month, a week (a day in a narrow screen). Filters are kept. */
export function shiftState(state: AgendaState, by: -1 | 1, narrow = false): AgendaState {
  switch (state.view) {
    case 'month': {
      const index = state.year * 12 + (state.month ?? 1) - 1 + by;
      return { ...state, year: Math.floor(index / 12), month: (index % 12) + 1 };
    }
    case 'week': {
      const date = addDays(state.date ?? toDateString(state.year, 1, 1), by * (narrow ? 1 : 7));
      return { ...state, year: parseLocal(date).year, date };
    }
    default:
      return { ...state, year: state.year + by, month: null };
  }
}

/** "Hoy": the period that holds the given local date. */
export function todayState(state: AgendaState, today: string): AgendaState {
  const { year, month } = parseLocal(today);
  switch (state.view) {
    case 'month':
      return { ...state, year, month };
    case 'week':
      return { ...state, year, date: today };
    default:
      return { ...state, year, month: null };
  }
}

/**
 * The state after choosing a view, keeping what can be kept: the week of a month is its first day (or today if it is inside it),
 * the month of a week is the one it is on, and the year always carries over. The year list always opens on the whole year.
 */
export function viewState(state: AgendaState, view: AgendaView, today: string): AgendaState {
  const now = parseLocal(today);
  switch (view) {
    case 'week': {
      const date =
        state.date ??
        (state.month !== null
          ? now.year === state.year && now.month === state.month
            ? today
            : toDateString(state.year, state.month, 1)
          : now.year === state.year
            ? today
            : toDateString(state.year, 1, 1));
      return { ...state, view, year: parseLocal(date).year, month: null, date };
    }
    case 'month': {
      const month = state.date
        ? parseLocal(state.date).month
        : (state.month ?? defaultMonth(state.year, today));
      return { ...state, view, month, date: null };
    }
    case 'year':
      return { ...state, view, month: null, date: null };
    default:
      // The year list shows the whole year; narrowing it to a month is up to the person (the summary), not something the month view leaves behind.
      return { ...state, view, month: null, date: null };
  }
}

/** The title of the period: "2026", "Octubre de 2026", "26 oct – 1 nov 2026" or, for the one day of a narrow week, "sáb 24 oct 2026". */
export function periodTitle(state: AgendaState, narrow = false): string {
  if (state.view === 'month') {
    return `${capitalize(monthName(state.month ?? 1))} de ${state.year}`;
  }
  if (state.view === 'week' && state.date) {
    if (narrow) {
      return `${shortDate(state.date)} ${parseLocal(state.date).year}`;
    }
    const { from } = weekRange(state.date);
    const last = addDays(from, 6);
    const start = parseLocal(from);
    const end = parseLocal(last);
    const label = (date: string) => shortDate(date).split(' ').slice(1).join(' ');
    return start.year === end.year
      ? `${label(from)} – ${label(last)} ${end.year}`
      : `${label(from)} ${start.year} – ${label(last)} ${end.year}`;
  }
  return String(state.year);
}
