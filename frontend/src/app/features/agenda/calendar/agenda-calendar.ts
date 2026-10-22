import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  computed,
  effect,
  input,
  output,
  untracked,
  viewChild,
} from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import type {
  CalendarOptions,
  DateClickInfo,
  DatesSetInfo,
  EventClickInfo,
} from '@fullcalendar/angular';
import { FullCalendarComponent, FullCalendarModule } from '@fullcalendar/angular';
import dayGridPlugin from '@fullcalendar/angular/daygrid';
import interactionPlugin from '@fullcalendar/angular/interaction';
import esLocale from 'fullcalendar/locales/es';
import multiMonthPlugin from '@fullcalendar/angular/multimonth';
import classicTheme from '@fullcalendar/angular/themes/classic';
import timeGridPlugin from '@fullcalendar/angular/timegrid';
import { overduePresentation, statusPresentation } from '../data/agenda-labels';
import { CalendarView, weekStart } from '../data/agenda-state';
import {
  capitalize,
  dateOf,
  instantToLocalInput,
  monthName,
  timeOf,
  todayIn,
} from '../data/agenda-time';
import { EventStatus, EventSummary } from '../data/agenda.models';
import {
  CalendarPosition,
  CalendarSlot,
  agendaView,
  fullCalendarView,
  toCalendarEvents,
} from './calendar-model';

/**
 * What a link to a day or a month says to a screen reader. FullCalendar gives the long date for a day ("14 de octubre de 2026"), but
 * for a month title only its ISO form ("2026-10").
 */
function navLinkHint(text: string): string {
  const month = /^(\d{4})-(\d{2})$/.exec(text);
  return month ? `Ir a ${capitalize(monthName(Number(month[2])))} de ${month[1]}` : `Ir al ${text}`;
}

/** Fixed options: the connector compares options by identity, so the ones that never change are not rebuilt on every change of an input. */
const PLUGINS = [classicTheme, dayGridPlugin, timeGridPlugin, multiMonthPlugin, interactionPlugin];
const EVENT_TIME_FORMAT = { hour: '2-digit', minute: '2-digit', hour12: false } as const;

const LEGEND: readonly EventStatus[] = ['draft', 'scheduled', 'completed', 'cancelled'];

/**
 * The year, month and week views of the agenda (SPEC-0006 RF-12, §10.4), drawn by FullCalendar under the Material theme. It is read
 * only: it never moves an activity (Q-18). The page owns the state (view, period and filters, kept in the address) and this
 * component only draws it: when FullCalendar changes view by itself (a month title or a day is chosen) it says so and waits for the
 * page to answer. Lazy loaded with the page's calendar views, so the year list does not pay for it.
 */
@Component({
  selector: 'app-agenda-calendar',
  imports: [FullCalendarModule, MatIconModule],
  templateUrl: './agenda-calendar.html',
  styleUrl: './agenda-calendar.scss',
  // FullCalendar draws outside Angular's view: its content takes the style of this component, so it cannot be encapsulated.
  encapsulation: ViewEncapsulation.None,
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AgendaCalendar {
  readonly view = input.required<CalendarView>();
  /** The day the view is on: January 1st for the year, the 1st for the month, any day of the week. `yyyy-MM-dd`. */
  readonly date = input.required<string>();
  readonly events = input.required<readonly EventSummary[]>();
  /** The IANA zone of the organization: every time is drawn in it, whatever the zone of the browser is (RNF-9). */
  readonly timeZone = input.required<string>();
  /** A screen narrower than 600 px: the week is one day and the year, one column of months (§10.4). */
  readonly narrow = input(false);
  /** With `agenda.create`, an empty day or hour still to come plans an activity (RF-12b); a past one does nothing (RN-16). */
  readonly canCreate = input(false);
  /** A fixed "now" for the line of the current time and "today" (tests); the clock otherwise. */
  readonly now = input<string | null>(null);

  readonly eventOpen = output<string>();
  readonly positionChange = output<CalendarPosition>();
  readonly slotChoose = output<CalendarSlot>();

  protected readonly calendar = viewChild(FullCalendarComponent);

  protected readonly legend = LEGEND.map((status) => statusPresentation(status));
  protected readonly overdue = overduePresentation();

  protected readonly calendarEvents = computed(() => toCalendarEvents(this.events()));

  protected readonly ariaLabel = computed(() => {
    const labels: Record<CalendarView, string> = {
      year: 'Calendario del año',
      month: 'Calendario del mes',
      week: this.narrow() ? 'Calendario del día' : 'Calendario de la semana',
    };
    return labels[this.view()];
  });

  /**
   * The options. The view and the day it opens on are read once (FullCalendar ignores them afterwards: the effect below moves it);
   * the events come through their own input. Everything else follows the inputs.
   */
  protected readonly options = computed<CalendarOptions>(() => {
    const narrow = this.narrow();
    const type = fullCalendarView(this.view(), narrow);
    const initial = untracked(() => this.date());
    return {
      plugins: PLUGINS,
      locale: esLocale,
      timeZone: this.timeZone(),
      initialView: type,
      initialDate: initial,
      now: this.now() ?? undefined,
      firstDay: 1,
      headerToolbar: false,
      editable: false,
      // A day number or a month title leads to its week or month in the year and month views; the week has nowhere else to go.
      navLinks: type === 'multiMonthYear' || type === 'dayGridMonth',
      nowIndicator: true,
      fixedWeekCount: false,
      dayMaxEvents: true,
      allDaySlot: true,
      slotMinTime: '00:00:00',
      slotMaxTime: '24:00:00',
      scrollTime: '06:00:00',
      expandRows: true,
      height: type === 'timeGridWeek' || type === 'timeGridDay' ? 720 : 'auto',
      multiMonthMaxColumns: narrow ? 1 : 3,
      eventTimeFormat: EVENT_TIME_FORMAT,
      navLinkHint: (text: string) => navLinkHint(text),
      eventClick: (info: EventClickInfo) => this.openEvent(info),
      dateClick: (info: DateClickInfo) => this.chooseDate(info),
      datesSet: (info: DatesSetInfo) => this.followCalendar(info),
    };
  });

  constructor() {
    // Our state is the source of truth: when the page changes the view or the day, FullCalendar is taken there.
    effect(() => {
      const api = this.calendar()?.getApi();
      const type = fullCalendarView(this.view(), this.narrow());
      const date = this.date();
      if (!api) {
        return;
      }
      untracked(() => {
        if (api.view.type !== type) {
          api.changeView(type, date);
        } else {
          api.gotoDate(date);
        }
      });
    });
  }

  private openEvent(info: EventClickInfo): void {
    const click = info.jsEvent;
    // With Ctrl, Cmd or Shift (or another button) the link opens where the person asked: a new tab or window.
    if (click.ctrlKey || click.metaKey || click.shiftKey || click.altKey || click.button !== 0) {
      return;
    }
    // A plain click goes through the router instead of loading the page again.
    click.preventDefault();
    this.eventOpen.emit(info.event.id);
  }

  private chooseDate(info: DateClickInfo): void {
    const date = dateOf(info.dateStr);
    if (this.view() === 'year') {
      // A day of the year view opens its week (RF-12).
      this.positionChange.emit({ view: 'week', date });
    } else if (this.canCreate() && !this.isPast(info)) {
      this.slotChoose.emit({ date, time: info.allDay ? null : timeOf(info.dateStr) });
    }
  }

  /**
   * Whether a day or an hour already passed in the zone of the organization: an activity cannot start in the past (RN-16), so the
   * calendar does not offer to plan it there instead of opening a form that would refuse it. Today is still offered as a day.
   */
  private isPast(info: DateClickInfo): boolean {
    const now = this.nowLocal();
    return info.allDay
      ? dateOf(info.dateStr) < now.slice(0, 10)
      : `${dateOf(info.dateStr)}T${timeOf(info.dateStr)}` <= now;
  }

  /**
   * "Now" as the local date and time of the organization (`yyyy-MM-ddTHH:mm`). A fixed `now` without an offset is already local to
   * it, as FullCalendar reads it; with an offset, or the clock, it is converted.
   */
  private nowLocal(): string {
    const fixed = this.now();
    if (fixed && !/(Z|[+-]\d{2}:\d{2})$/.test(fixed)) {
      return fixed.slice(0, 16);
    }
    return instantToLocalInput(fixed ?? new Date().toISOString(), this.timeZone());
  }

  /**
   * FullCalendar may change view by itself (a month title or a day of the year view is chosen, a day number of the month view):
   * when it ends up somewhere our state is not, the page is told and navigates, and the effect then keeps both in step.
   */
  private followCalendar(info: DatesSetInfo): void {
    const view = agendaView(info.view.type);
    if (!view) {
      return;
    }
    // The first day of what it shows (the year, the month, the week or the day), in the zone of the organization.
    const start = todayIn(this.timeZone(), info.view.currentStart);
    const date = this.date();
    const inStep =
      view === this.view() &&
      (view === 'year'
        ? start.slice(0, 4) === date.slice(0, 4)
        : view === 'month'
          ? start.slice(0, 7) === date.slice(0, 7)
          : info.view.type === 'timeGridDay'
            ? start === date
            : start === weekStart(date));
    if (!inStep) {
      this.positionChange.emit({ view, date: start });
    }
  }
}
