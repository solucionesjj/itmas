import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { SelectionModel } from '@angular/cdk/collections';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  Observable,
  Subject,
  catchError,
  combineLatest,
  map,
  of,
  share,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { IfPermission } from '../../../auth/directives/if-permission';
import { AuthService } from '../../../auth/services/auth-service';
import { AgendaCalendar } from '../calendar/agenda-calendar';
import { CalendarPosition, CalendarSlot, anchorDate } from '../calendar/calendar-model';
import { AgendaFiltersBar, PersonFilterOption } from '../components/filters-bar';
import { EventStatusChips } from '../components/event-status-chips';
import { OutlookStatusIndicator } from '../components/outlook-status-indicator';
import { YearSummary } from '../components/year-summary';
import { AgendaApi, AgendaFilters } from '../data/agenda-api';
import { impactLabel } from '../data/agenda-labels';
import {
  AVAILABLE_VIEWS,
  AgendaState,
  AgendaView,
  CalendarView,
  exportTarget,
  hasFilters,
  isCalendarView,
  parseAgendaState,
  periodTitle,
  shiftState,
  todayState,
  toQueryParams,
  viewState,
  visibleRange,
  weekRange,
} from '../data/agenda-state';
import { capitalize, monthName, parseLocal, todayIn, whenLabel } from '../data/agenda-time';
import { EventList, EventSummary, EventType, LIMITS, Summary } from '../data/agenda.models';
import { AgendaActions } from '../events/agenda-actions';

/** A row of the year list: a month heading or an activity. */
type Row =
  | { readonly kind: 'month'; readonly key: string; readonly label: string; readonly count: number }
  | { readonly kind: 'event'; readonly event: EventSummary };

const VIEW_LABELS: Record<AgendaView, string> = {
  list: 'Lista del año',
  year: 'Año',
  month: 'Mes',
  week: 'Semana',
};

/** The zone of the organization until the first answer of the API says it (it is configured, SPEC-0006 RNF-4). */
const DEFAULT_ZONE = 'America/Bogota';

/** What "Anterior" and "Siguiente" move, by view (RF-12). */
const STEP_LABELS: Record<AgendaView, string> = {
  list: 'año',
  year: 'año',
  month: 'mes',
  week: 'semana',
};

/**
 * The agenda (SPEC-0006 RF-12, RF-13, §11). The view, the period and the filters live in the address. It offers the year list
 * (every activity of the year grouped by month and in chronological order, under the summary of the year) and the year, month
 * and week calendars, which share the filters. The list stays as the complete, accessible alternative to the calendars (RNF-10).
 */
@Component({
  selector: 'app-agenda-page',
  imports: [
    AgendaCalendar,
    AgendaFiltersBar,
    EventStatusChips,
    IfPermission,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatChipsModule,
    MatIconModule,
    MatMenuModule,
    MatProgressBarModule,
    MatTableModule,
    MatTooltipModule,
    OutlookStatusIndicator,
    RouterLink,
    YearSummary,
  ],
  templateUrl: './agenda-page.html',
  styleUrl: '../agenda-page.scss',
})
export class AgendaPage {
  private readonly api = inject(AgendaApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly actions = inject(AgendaActions);
  protected readonly auth = inject(AuthService);

  /** The zone of the organization, as the API last said it; every date of the agenda is read in it (RNF-9). */
  private readonly zone = signal(DEFAULT_ZONE);

  /** The year shown when the address does not say: the current one in the zone of the organization, not in the one of the browser. */
  private get defaultYear(): number {
    return parseLocal(this.todayDate()).year;
  }

  /** A screen narrower than 600 px: the week is one day at a time and the year, one column of months (§10.4). */
  protected readonly narrow = toSignal(
    inject(BreakpointObserver)
      .observe(Breakpoints.XSmall)
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  protected readonly state = toSignal(
    this.route.queryParamMap.pipe(
      map((params) => parseAgendaState(params, this.defaultYear, this.todayDate())),
    ),
    {
      initialValue: parseAgendaState(
        { get: () => null, getAll: () => [] },
        this.defaultYear,
        this.todayDate(),
      ),
    },
  );

  /** The calendar view being shown, or null for the year list. */
  protected readonly calendarView = computed<CalendarView | null>(() => {
    const view = this.state().view;
    return isCalendarView(view) ? view : null;
  });
  protected readonly anchor = computed(() => anchorDate(this.state()));
  protected readonly timeZone = computed(() => this.list()?.timeZone ?? this.zone());
  protected readonly events = computed(() => this.list()?.items ?? []);

  protected readonly views = AVAILABLE_VIEWS;
  protected readonly viewLabels = VIEW_LABELS;
  protected readonly columns = [
    'select',
    'when',
    'type',
    'title',
    'responsible',
    'status',
    'outlook',
    'actions',
  ];
  protected readonly monthColumns = ['month'];

  protected readonly list = signal<EventList | null>(null);
  protected readonly summary = signal<Summary | null>(null);
  protected readonly types = signal<readonly EventType[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  /** An `.ics` export is being downloaded (RF-21). */
  protected readonly exporting = signal(false);
  /** People seen so far in the lists: they feed the "Responsable" filter. */
  private readonly knownPeople = signal<readonly PersonFilterOption[]>([]);
  protected readonly selection = new SelectionModel<string>(true, []);
  private readonly selectionTick = signal(0);
  private readonly reload = new Subject<void>();

  protected readonly canPublish = computed(() => this.auth.hasPermission('agenda.publish'));
  protected readonly canCreate = computed(() => this.auth.hasPermission('agenda.create'));
  /** A past year cannot be planned (RN-16): the buttons that create activities are offered for the current year on. */
  protected readonly offerCreate = computed(
    () => this.canCreate() && this.state().year >= this.defaultYear,
  );
  protected readonly outlookEnabled = computed(() => this.list()?.outlookEnabled ?? false);
  protected readonly filtersOn = computed(() => hasFilters(this.state().filters));
  protected readonly people = computed(() => {
    const selected = this.state().filters.responsible;
    const known = this.knownPeople();
    return selected && !known.some((person) => person.id === selected)
      ? [...known, { id: selected, name: 'Responsable elegido' }]
      : known;
  });

  /** Every row of the list: the activities grouped under their month, narrowed to the chosen month if any. */
  protected readonly rows = computed<readonly Row[]>(() => {
    const items = this.list()?.items ?? [];
    const month = this.state().month;
    const year = this.state().year;
    const rows: Row[] = [];
    const groups = new Map<string, EventSummary[]>();
    for (const item of items) {
      const key = item.start.slice(0, 7);
      if (month !== null && Number(key.slice(5, 7)) !== month) {
        continue;
      }
      groups.set(key, [...(groups.get(key) ?? []), item]);
    }
    for (const [key, events] of groups) {
      const keyYear = Number(key.slice(0, 4));
      const name = capitalize(monthName(Number(key.slice(5, 7))));
      rows.push({
        kind: 'month',
        key,
        label: keyYear === year ? name : `${name} de ${keyYear}`,
        count: events.length,
      });
      rows.push(...events.map((event) => ({ kind: 'event', event }) as const));
    }
    return rows;
  });

  protected readonly eventCount = computed(
    () => this.rows().filter((row) => row.kind === 'event').length,
  );

  /** Drafts shown (the only ones that can be scheduled in a batch). */
  private readonly selectableDrafts = computed(() =>
    this.rows().flatMap((row) =>
      row.kind === 'event' && row.event.status === 'draft' ? [row.event] : [],
    ),
  );
  protected readonly selectedCount = computed(
    () => (this.selectionTick(), this.selection.selected.length),
  );
  protected readonly allSelected = computed(
    () =>
      this.selectableDrafts().length > 0 && this.selectedCount() === this.selectableDrafts().length,
  );
  protected readonly tooManySelected = computed(() => this.selectedCount() > LIMITS.batch);
  protected readonly batchLimit = LIMITS.batch;

  /** The period an export covers: the week, the month the view is on or the list is narrowed to, or else the year. */
  private readonly exportPeriod = computed(() => {
    const { year, month, date, view } = this.state();
    if (view === 'week' && date) {
      const from = weekRange(date).from;
      return `la semana del ${parseLocal(from).day} de ${monthName(parseLocal(from).month)} de ${parseLocal(from).year}`;
    }
    return month === null ? String(year) : `${monthName(month)} de ${year}`;
  });
  protected readonly exportLabel = computed(() =>
    this.exporting()
      ? 'Exportando las actividades programadas…'
      : `Exportar .ics de las actividades programadas de ${this.exportPeriod()}`,
  );
  protected readonly exportHint = computed(() => {
    const status = this.state().filters.status;
    const note =
      status !== '' && status !== 'scheduled'
        ? ' El filtro de estado no se aplica: solo se exportan las programadas.'
        : '';
    return `Descarga las actividades programadas de ${this.exportPeriod()}, con los filtros activos, en un archivo .ics para Outlook u otro calendario.${note}`;
  });

  /** What the calendars say when the period has no activities (§8.4): the calendar itself is still drawn. */
  protected readonly calendarEmptyMessage = computed(() =>
    this.filtersOn()
      ? 'Ninguna actividad coincide con los filtros.'
      : 'No hay actividades en este periodo.',
  );

  protected readonly emptyMessage = computed(() => {
    if (this.loading()) {
      return 'Cargando…';
    }
    // In the year list the month is a filter; in the month view it is the period.
    if (this.filtersOn() || (this.state().view === 'list' && this.state().month !== null)) {
      return 'Ninguna actividad coincide con los filtros.';
    }
    return this.state().year >= this.defaultYear
      ? 'Todavía no hay actividades planificadas.'
      : 'No hay actividades en este periodo.';
  });

  constructor() {
    this.api
      .listTypes()
      .pipe(
        catchError(() => of([] as readonly EventType[])),
        takeUntilDestroyed(),
      )
      .subscribe((types) => this.types.set(types));

    // One shared query for the list and the summary: the loading and failure flags are reset once per request, so a response that
    // arrives at once cannot be overwritten by the second subscriber resetting them again.
    const query = combineLatest([
      toObservable(this.state),
      this.reload.pipe(startWith(undefined)),
    ]).pipe(
      map(([state]) => state),
      tap(() => {
        this.loading.set(true);
        this.failed.set(false);
      }),
      share(),
    );

    query
      .pipe(
        switchMap((state) => {
          const { from, to } = visibleRange(state);
          return this.api.list(from, to, state.filters).pipe(
            catchError(() => {
              this.failed.set(true);
              return of(null);
            }),
          );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((list) => {
        this.list.set(list);
        if (list) {
          this.zone.set(list.timeZone);
        }
        this.loading.set(false);
        this.selection.clear();
        this.selectionTick.update((tick) => tick + 1);
        if (list) {
          this.rememberPeople(list.items);
        }
      });

    // The summary heads the year list only (RF-13): the calendars do not ask for it.
    query
      .pipe(
        switchMap((state) =>
          state.view === 'list'
            ? this.api.summary(state.year, state.filters).pipe(catchError(() => of(null)))
            : of(null),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((summary) => this.summary.set(summary));
  }

  protected isMonth = (_: number, row: Row): boolean => row.kind === 'month';
  protected isEvent = (_: number, row: Row): boolean => row.kind === 'event';

  protected readonly periodTitle = computed(() => periodTitle(this.state(), this.narrow()));

  /** "Anterior" and "Siguiente" say what they move: a year, a month, a week or, in a narrow screen, a day. */
  private readonly step = computed(() =>
    capitalize(
      this.state().view === 'week' && this.narrow() ? 'día' : STEP_LABELS[this.state().view],
    ),
  );
  protected readonly previousLabel = computed(() => `${this.step()} anterior`);
  protected readonly nextLabel = computed(() => `${this.step()} siguiente`);

  protected whenOf = whenLabel;
  protected impactOf = impactLabel;

  // ---- Address state --------------------------------------------------------------------------------------------

  protected setFilters(filters: AgendaFilters): void {
    this.navigate({ ...this.state(), filters, month: this.state().month });
  }

  protected setYear(year: number): void {
    this.navigate({ ...this.state(), year, month: null });
  }

  protected setMonth(month: number | null): void {
    this.navigate({ ...this.state(), month });
  }

  protected setView(view: AgendaView): void {
    this.navigate(viewState(this.state(), view, this.todayDate()));
  }

  protected shift(by: -1 | 1): void {
    this.navigate(shiftState(this.state(), by, this.narrow()));
  }

  protected today(): void {
    this.navigate(todayState(this.state(), this.todayDate()));
  }

  /** The calendar went somewhere by itself (a month title, a day): the address follows (RF-12). */
  protected goTo(position: CalendarPosition): void {
    const { year, month } = parseLocal(position.date);
    const state = this.state();
    this.navigate(
      position.view === 'week'
        ? { ...state, view: 'week', year, month: null, date: position.date }
        : position.view === 'month'
          ? { ...state, view: 'month', year, month, date: null }
          : { ...state, view: 'year', year, month: null, date: null },
    );
  }

  /** An empty day or hour of the calendar opens "Nueva actividad" with it already filled (RF-12b, P-21). */
  protected plan(slot: CalendarSlot): void {
    void this.router.navigate(['/agenda/events/new'], {
      queryParams: { date: slot.date, time: slot.time },
    });
  }

  protected openById(id: string): void {
    void this.router.navigate(['/agenda/events', id]);
  }

  /** "Today" in the zone of the organization, `yyyy-MM-dd`. */
  private todayDate(): string {
    return todayIn(this.zone());
  }

  protected filterByTag(tag: string): void {
    const filters = this.state().filters;
    if (!filters.tags.some((existing) => existing.toLowerCase() === tag.toLowerCase())) {
      this.setFilters({ ...filters, tags: [...filters.tags, tag] });
    }
  }

  protected clearFilters(): void {
    const state = this.state();
    this.navigate({
      ...state,
      // The month view needs its month; the list is narrowed by it, so clearing the filters shows the whole year again.
      month: state.view === 'month' ? state.month : null,
      filters: { type: '', status: '', responsible: '', mine: false, tags: [], search: '' },
    });
  }

  // ---- Selection and actions -------------------------------------------------------------------------------------

  protected toggleAll(): void {
    if (this.allSelected()) {
      this.selection.clear();
    } else {
      this.selection.select(...this.selectableDrafts().map((event) => event.id));
    }
    this.selectionTick.update((tick) => tick + 1);
  }

  protected toggle(event: EventSummary): void {
    this.selection.toggle(event.id);
    this.selectionTick.update((tick) => tick + 1);
  }

  protected isSelected(event: EventSummary): boolean {
    return (this.selectionTick(), this.selection.isSelected(event.id));
  }

  protected scheduleSelected(): void {
    const targets = this.selectableDrafts().filter((event) => this.selection.isSelected(event.id));
    this.run(this.actions.schedule(targets, this.outlookEnabled()));
  }

  protected schedule(event: EventSummary): void {
    this.run(this.actions.schedule([event], this.outlookEnabled()));
  }

  protected cancel(event: EventSummary): void {
    this.run(this.actions.cancel(event, this.outlookEnabled()));
  }

  protected complete(event: EventSummary): void {
    this.run(this.actions.complete(event, this.list()?.timeZone ?? 'America/Bogota', null));
  }

  protected deleteDraft(event: EventSummary): void {
    this.run(this.actions.deleteDraft(event));
  }

  protected sync(event: EventSummary): void {
    this.run(this.actions.sync(event));
  }

  protected syncAll(): void {
    this.run(this.actions.syncAll());
  }

  protected open(event: EventSummary): void {
    void this.router.navigate(['/agenda/events', event.id]);
  }

  protected retry(): void {
    this.reload.next();
  }

  /** Downloads the scheduled activities of the period and the active filters as an `.ics` file (RF-21). */
  protected exportCalendar(): void {
    if (this.exporting()) {
      return;
    }
    this.exporting.set(true);
    this.actions
      .exportCalendar(exportTarget(this.state()), this.state().filters)
      .subscribe(() => this.exporting.set(false));
  }

  /** Changing a scheduled activity notifies its guests, so it needs `agenda.publish` as well (P-12). */
  protected canEdit(event: EventSummary): boolean {
    return (
      this.auth.hasPermission('agenda.update') &&
      (event.status === 'draft' || (event.status === 'scheduled' && this.canPublish()))
    );
  }

  protected canComplete(event: EventSummary): boolean {
    return this.auth.hasPermission('agenda.complete') && event.status === 'scheduled';
  }

  /** An execution can be recorded from the scheduled start on (RN-8): before it the menu item is disabled. */
  protected hasStarted(event: EventSummary): boolean {
    return Date.parse(event.startUtc) <= Date.now();
  }

  /** "Sincronizar de nuevo" only makes sense for a scheduled activity Outlook has not confirmed, with Outlook on. */
  protected canSync(event: EventSummary): boolean {
    return (
      this.canPublish() &&
      this.outlookEnabled() &&
      event.status === 'scheduled' &&
      event.outlook.status !== 'published'
    );
  }

  protected hasActions(event: EventSummary): boolean {
    return (
      this.canEdit(event) ||
      (this.canPublish() && (event.status === 'draft' || event.status === 'scheduled')) ||
      this.canComplete(event)
    );
  }

  protected ariaOf(event: EventSummary): string {
    return `${event.title}, ${event.type.name}, ${this.whenOf(event)}`;
  }

  private run(operation: Observable<unknown>): void {
    operation.subscribe(() => this.reload.next());
  }

  private navigate(state: AgendaState): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: toQueryParams(state, this.defaultYear),
    });
  }

  private rememberPeople(items: readonly EventSummary[]): void {
    const known = new Map(this.knownPeople().map((person) => [person.id, person]));
    for (const item of items) {
      known.set(item.responsible.id, { id: item.responsible.id, name: item.responsible.name });
    }
    this.knownPeople.set([...known.values()].sort((a, b) => a.name.localeCompare(b.name, 'es')));
  }
}
