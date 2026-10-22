import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { FullCalendarComponent } from '@fullcalendar/angular';
import { summaryOf } from '../data/agenda-fixtures';
import { CalendarView } from '../data/agenda-state';
import { EventSummary } from '../data/agenda.models';
import { AgendaCalendar } from './agenda-calendar';
import { CalendarPosition, CalendarSlot } from './calendar-model';

/**
 * The calendar views of SPEC-0006 (RF-12, CA-31 to CA-34) with FullCalendar 7 in jsdom: zoneless, with signal inputs. jsdom has no
 * layout, so what depends on measuring (how many months fit in a row, the "+n más" cut, the colours) is checked in the browser and
 * recorded in the traceability of the spec.
 */
describe('AgendaCalendar (CA-31 to CA-34)', () => {
  const ZONE = 'America/Bogota';

  beforeAll(() => {
    // jsdom has no ResizeObserver, which FullCalendar uses to measure itself.
    (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver ??= class {
      observe(): void {}
      unobserve(): void {}
      disconnect(): void {}
    };
  });

  interface Inputs {
    view: CalendarView;
    date: string;
    events: readonly EventSummary[];
    timeZone: string;
    narrow: boolean;
    canCreate: boolean;
    now: string | null;
  }

  async function mount(inputs: Partial<Inputs> & Pick<Inputs, 'view' | 'date'>) {
    const fixture = TestBed.createComponent(AgendaCalendar);
    const all: Inputs = {
      events: [],
      timeZone: ZONE,
      narrow: false,
      canCreate: false,
      now: null,
      ...inputs,
    };
    for (const [name, value] of Object.entries(all)) {
      fixture.componentRef.setInput(name, value);
    }
    const opened: string[] = [];
    const moved: CalendarPosition[] = [];
    const slots: CalendarSlot[] = [];
    fixture.componentInstance.eventOpen.subscribe((id) => opened.push(id));
    fixture.componentInstance.positionChange.subscribe((position) => moved.push(position));
    fixture.componentInstance.slotChoose.subscribe((slot) => slots.push(slot));
    await settle(fixture);
    return { fixture, opened, moved, slots };
  }

  const settle = async (fixture: ComponentFixture<unknown>) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  /**
   * Runs `body` with the browser in `zone`, checking first that the change took effect: a test of RNF-9 must not pass because
   * the zone stayed the one of the machine. The rest of the suite runs in Asia/Tokyo (src/test-setup.ts).
   */
  async function inBrowserZone(zone: string, body: () => Promise<void>): Promise<void> {
    const previous = process.env['TZ'];
    try {
      process.env['TZ'] = zone;
      expect(Intl.DateTimeFormat().resolvedOptions().timeZone).toBe(zone);
      await body();
    } finally {
      if (previous === undefined) {
        delete process.env['TZ'];
      } else {
        process.env['TZ'] = previous;
      }
    }
  }

  const api = (fixture: ComponentFixture<unknown>) =>
    fixture.debugElement.query(By.directive(FullCalendarComponent))
      .componentInstance as FullCalendarComponent;

  const all = (fixture: ComponentFixture<unknown>, selector: string) => [
    ...(fixture.nativeElement as HTMLElement).querySelectorAll(selector),
  ];

  /** The short label of each column of a week ("lun 19"): the header also carries the long date for a screen reader. */
  const columns = (fixture: ComponentFixture<unknown>) =>
    all(fixture, '[role=columnheader]').map(
      (header) => /\p{L}{3} \d{1,2}$/u.exec(text(header))?.[0],
    );

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';

  const cell = (fixture: ComponentFixture<unknown>, date: string) =>
    (fixture.nativeElement as HTMLElement).querySelector(`[role=cell][data-date="${date}"]`);

  const eventsOn = (fixture: ComponentFixture<unknown>, date: string) =>
    [...(cell(fixture, date)?.querySelectorAll('a[href]') ?? [])].map(text);

  const patching = (overrides: Partial<EventSummary> = {}) =>
    summaryOf({ id: 'e1', title: 'Parches de octubre', status: 'scheduled', ...overrides });

  it('draws the month grid from Monday to Sunday with the days of the neighbouring months (CA-32)', async () => {
    const { fixture } = await mount({ view: 'month', date: '2026-10-01' });

    expect(
      all(fixture, '[role=columnheader]').map((header) => text(header.firstElementChild)),
    ).toEqual(['lunes', 'martes', 'miércoles', 'jueves', 'viernes', 'sábado', 'domingo']);
    const days = all(fixture, '[role=cell][data-date]').map((day) => day.getAttribute('data-date'));
    expect(days[0]).toBe('2026-09-28');
    expect(days.at(-1)).toBe('2026-11-01');
    expect(days).toHaveLength(35);
  });

  it('shows each activity on its day with its time, type, title and state in text (CA-32, CA-34)', async () => {
    const { fixture } = await mount({ view: 'month', date: '2026-10-01', events: [patching()] });

    const [event] = all(fixture, 'a[href="/agenda/events/e1"]');
    expect(text(event)).toContain('09:00');
    expect(text(event)).toContain('Parches de octubre');
    expect(text(event)).toContain('Mantenimiento y parches de sistema operativo');
    expect(text(event)).toContain('Programada');
    expect(event.querySelector('mat-icon')?.textContent).toBe('event_available');
    expect(event.querySelector('mat-icon')?.getAttribute('aria-hidden')).toBe('true');
    expect(eventsOn(fixture, '2026-10-24')).toHaveLength(1);
  });

  it('draws an all-day activity of several days on its days, ending on the last one (CA-32)', async () => {
    const event = patching({ id: 'wide', isAllDay: true, start: '2026-10-26', end: '2026-10-28' });
    const { fixture } = await mount({ view: 'month', date: '2026-10-01', events: [event] });

    const drawn = api(fixture).getApi().getEventById('wide')!;
    expect(drawn.allDay).toBe(true);
    expect(drawn.startStr).toBe('2026-10-26');
    expect(drawn.endStr).toBe('2026-10-29');
    expect(all(fixture, 'a[href="/agenda/events/wide"]')).toHaveLength(1);
  });

  it('offers "+n más" when many activities share a day (CA-32)', async () => {
    const crowded = Array.from({ length: 8 }, (_, index) =>
      patching({
        id: `c${index}`,
        title: `Tarea ${index}`,
        start: `2026-10-22T0${index + 1}:00:00`,
        end: `2026-10-22T0${index + 1}:30:00`,
      }),
    );
    const { fixture } = await mount({ view: 'month', date: '2026-10-01', events: crowded });

    expect(text(fixture.nativeElement)).toMatch(/\+\d+ más/);
  });

  it('draws the week with seven columns from Monday, the hours axis and the all-day strip (CA-33)', async () => {
    const { fixture } = await mount({ view: 'week', date: '2026-10-21' });

    expect(columns(fixture)).toEqual([
      'lun 19',
      'mar 20',
      'mié 21',
      'jue 22',
      'vie 23',
      'sáb 24',
      'dom 25',
    ]);
    expect(all(fixture, '[role=rowheader]').map(text)).toContain('Todo el día');
  });

  it('places the activities by hour and duration and puts the simultaneous ones side by side (CA-33)', async () => {
    const events = [
      patching({ id: 'a', title: 'A', start: '2026-10-21T10:00:00', end: '2026-10-21T12:00:00' }),
      patching({ id: 'b', title: 'B', start: '2026-10-21T10:30:00', end: '2026-10-21T11:30:00' }),
    ];
    const { fixture } = await mount({ view: 'week', date: '2026-10-21', events });

    const [a, b] = ['a', 'b'].map((id) => all(fixture, `a[href="/agenda/events/${id}"]`)[0]);
    expect(text(a)).toContain('10:00–12:00');
    expect(text(b)).toContain('10:30–11:30');
    expect(api(fixture).getApi().getEvents()).toHaveLength(2);
  });

  it('splits an activity that crosses midnight between the two days (CA-33)', async () => {
    const event = patching({
      id: 'night',
      start: '2026-10-24T22:00:00',
      end: '2026-10-25T02:00:00',
    });
    const { fixture } = await mount({ view: 'week', date: '2026-10-24', events: [event] });

    const parts = all(fixture, 'a[href="/agenda/events/night"]').map(text);
    expect(parts).toHaveLength(2);
    expect(parts[0]).toContain('22:00');
    expect(parts[1]).toContain('2:00');
  });

  it('puts an all-day activity in the strip, with no hour (CA-33)', async () => {
    const event = patching({
      id: 'day',
      isAllDay: true,
      start: '2026-10-20',
      end: '2026-10-20',
      title: 'Todo el día',
    });
    const { fixture } = await mount({ view: 'week', date: '2026-10-21', events: [event] });

    const [drawn] = all(fixture, 'a[href="/agenda/events/day"]');
    expect(text(drawn)).not.toMatch(/\d{1,2}:\d{2}/);
    expect(api(fixture).getApi().getEventById('day')?.allDay).toBe(true);
  });

  it('marks the line of the current time, and only in the week that has it (CA-33)', async () => {
    const withNow = await mount({ view: 'week', date: '2026-10-21', now: '2026-10-21T10:30:00' });
    const without = await mount({ view: 'week', date: '2026-11-04', now: '2026-10-21T10:30:00' });

    expect(text(withNow.fixture.nativeElement)).toContain('Hora actual');
    expect(text(without.fixture.nativeElement)).not.toContain('Hora actual');
  });

  it('shows today in the zone of the organization with its own mark (CA-32, CA-34)', async () => {
    const { fixture } = await mount({
      view: 'week',
      date: '2026-10-21',
      now: '2026-10-21T23:30:00',
    });

    const current = all(fixture, '[role=columnheader][aria-current]');
    expect(current.map((header) => text(header).slice(-6))).toEqual(['mié 21']);
  });

  it('draws the twelve months of the year with the days that have activities (CA-31)', async () => {
    const events = [
      patching(),
      patching({ id: 'e2', start: '2026-03-10T09:00:00', end: '2026-03-10T10:00:00' }),
    ];
    const { fixture } = await mount({ view: 'year', date: '2026-01-01', events });

    expect(all(fixture, '[role=table][aria-labelledby]')).toHaveLength(12);
    expect(eventsOn(fixture, '2026-10-24')).toHaveLength(1);
    expect(eventsOn(fixture, '2026-03-10')).toHaveLength(1);
    expect(eventsOn(fixture, '2026-05-05')).toHaveLength(0);
  });

  it('opens the week of a day chosen in the year, with the mouse (CA-31)', async () => {
    const { fixture, moved } = await mount({ view: 'year', date: '2026-01-01' });
    const day = cell(fixture, '2026-10-24')?.querySelector('[role=link]') as HTMLElement;

    expect(day.getAttribute('tabindex')).toBe('0');
    day.click();

    await vi.waitFor(() => expect(moved).toEqual([{ view: 'week', date: '2026-10-24' }]));
  });

  it('opens the week of a day chosen in the year, with the keyboard (CA-31, RNF-10)', async () => {
    const { fixture, moved } = await mount({ view: 'year', date: '2026-01-01' });
    const day = cell(fixture, '2026-03-05')?.querySelector('[role=link]') as HTMLElement;

    day.dispatchEvent(
      new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }),
    );

    await vi.waitFor(() => expect(moved).toEqual([{ view: 'week', date: '2026-03-05' }]));
  });

  it('also opens the week from a day number of the month view', async () => {
    const { fixture, moved } = await mount({ view: 'month', date: '2026-10-01' });

    (cell(fixture, '2026-10-15')?.querySelector('[role=link]') as HTMLElement).click();

    await vi.waitFor(() => expect(moved).toEqual([{ view: 'week', date: '2026-10-15' }]));
  });

  it('opens the detail when an activity is chosen, as a link that the router handles (CA-34)', async () => {
    const { fixture, opened } = await mount({
      view: 'month',
      date: '2026-10-01',
      events: [patching()],
    });
    const link = all(fixture, 'a[href="/agenda/events/e1"]')[0];
    const click = new MouseEvent('click', { bubbles: true, cancelable: true });

    link.dispatchEvent(click);

    expect(opened).toEqual(['e1']);
    expect(click.defaultPrevented).toBe(true);
  });

  it('lets the person open an activity in another tab: with Ctrl, Cmd or Shift the link is left alone (CA-34)', async () => {
    const { fixture, opened } = await mount({
      view: 'month',
      date: '2026-10-01',
      events: [patching()],
    });
    const link = all(fixture, 'a[href="/agenda/events/e1"]')[0];
    // jsdom reports that it cannot follow a link: that is the browser's own default action, which is what is being left alone.
    vi.spyOn(console, 'error').mockImplementation(() => undefined);

    for (const modifier of [{ ctrlKey: true }, { metaKey: true }, { shiftKey: true }]) {
      const click = new MouseEvent('click', { bubbles: true, cancelable: true, ...modifier });
      link.dispatchEvent(click);
      expect(click.defaultPrevented).toBe(false);
    }
    expect(opened).toEqual([]);
  });

  it('has day and month links in the year and the month views only: the week has nowhere else to go (RF-12)', async () => {
    const options = async (view: CalendarView, date: string) =>
      api((await mount({ view, date })).fixture)
        .getApi()
        .getOption('navLinks');

    expect(await options('year', '2026-01-01')).toBe(true);
    expect(await options('month', '2026-10-01')).toBe(true);
    expect(await options('week', '2026-10-21')).toBe(false);
  });

  it('shows the full name of the activity as a tooltip: title, type, state and when (CA-34, RNF-10)', async () => {
    const { fixture } = await mount({ view: 'month', date: '2026-10-01', events: [patching()] });

    expect(all(fixture, 'a[href="/agenda/events/e1"] [title]')[0].getAttribute('title')).toBe(
      'Parches de octubre, Mantenimiento y parches de sistema operativo, Programada, sáb 24 oct · 09:00–11:00',
    );
  });

  it('reads the clock in the zone of the organization, not in the one of the browser (RNF-9)', async () => {
    await inBrowserZone('Asia/Tokyo', async () => {
      // 03:30 UTC is still the 21st (22:30) in Bogotá and already the 22nd (12:30) in Tokyo.
      const { fixture } = await mount({
        view: 'week',
        date: '2026-10-21',
        now: '2026-10-22T03:30:00Z',
      });

      expect(
        all(fixture, '[role=columnheader][aria-current]').map((header) => text(header).slice(-6)),
      ).toEqual(['mié 21']);
      expect(api(fixture).getApi().getOption('timeZone')).toBe('America/Bogota');
    });
  });

  it('is told by the page where to be, and does not answer back when it is already there (CA-34)', async () => {
    const { fixture, moved } = await mount({ view: 'month', date: '2026-10-01' });
    const year = () => api(fixture).getApi().view.currentStart.toISOString().slice(0, 4);

    fixture.componentRef.setInput('view', 'week');
    fixture.componentRef.setInput('date', '2026-10-21');
    await settle(fixture);
    expect(columns(fixture)[2]).toBe('mié 21');

    fixture.componentRef.setInput('view', 'year');
    fixture.componentRef.setInput('date', '2027-01-01');
    await settle(fixture);
    await vi.waitFor(() => expect(year()).toBe('2027'));
    expect(api(fixture).getApi().view.type).toBe('multiMonthYear');
    expect(all(fixture, '[role=table][aria-labelledby]')).toHaveLength(12);

    // FullCalendar tells its changes synchronously, so once it is where the page said, nothing else is coming.
    expect(moved).toEqual([]);
  });

  it('keeps the filters out of its business: it draws the events it is given and nothing else (CA-34)', async () => {
    const { fixture } = await mount({ view: 'month', date: '2026-10-01', events: [patching()] });

    fixture.componentRef.setInput('events', []);
    await settle(fixture);

    expect(all(fixture, 'a[href^="/agenda/events/"]')).toHaveLength(0);
  });

  it('plans an activity from an empty day or hour only with agenda.create (RF-12b, CA-34)', async () => {
    const now = '2026-10-14T08:00:00';
    const denied = await mount({ view: 'month', date: '2026-10-01', now });
    const allowed = await mount({ view: 'week', date: '2026-10-21', canCreate: true, now });
    const monthAllowed = await mount({ view: 'month', date: '2026-10-01', canCreate: true, now });
    const click = (fixture: ComponentFixture<unknown>, dateStr: string, allDay: boolean) =>
      (api(fixture).getApi().getOption('dateClick') as (info: unknown) => void)({
        dateStr,
        allDay,
      });

    click(denied.fixture, '2026-10-15', true);
    click(allowed.fixture, '2026-10-21T14:30:00-05:00', false);
    click(monthAllowed.fixture, '2026-10-15', true);

    expect(denied.slots).toEqual([]);
    expect(allowed.slots).toEqual([{ date: '2026-10-21', time: '14:30' }]);
    expect(monthAllowed.slots).toEqual([{ date: '2026-10-15', time: null }]);
  });

  it('does not offer to plan on a day or an hour that already passed in the zone of the organization (RN-16)', async () => {
    // 10:30 in Bogotá; the same instant is already the 22nd in UTC and 03:30 in the browser of someone in Europe.
    const month = await mount({
      view: 'month',
      date: '2026-10-01',
      canCreate: true,
      now: '2026-10-21T15:30:00Z',
    });
    const week = await mount({
      view: 'week',
      date: '2026-10-21',
      canCreate: true,
      now: '2026-10-21T10:30:00',
    });
    const click = (fixture: ComponentFixture<unknown>, dateStr: string, allDay: boolean) =>
      (api(fixture).getApi().getOption('dateClick') as (info: unknown) => void)({
        dateStr,
        allDay,
      });

    click(month.fixture, '2026-10-20', true);
    click(month.fixture, '2026-10-21', true);
    click(month.fixture, '2026-10-22', true);
    click(week.fixture, '2026-10-21T09:00:00-05:00', false);
    click(week.fixture, '2026-10-21T10:30:00-05:00', false);
    click(week.fixture, '2026-10-21T11:00:00-05:00', false);
    click(week.fixture, '2026-10-20', true);

    expect(month.slots).toEqual([
      { date: '2026-10-21', time: null },
      { date: '2026-10-22', time: null },
    ]);
    expect(week.slots).toEqual([{ date: '2026-10-21', time: '11:00' }]);
  });

  it('draws the hours of the organization whatever the zone of the browser is (RNF-9)', async () => {
    for (const zone of ['Pacific/Auckland', 'Asia/Tokyo', 'America/Los_Angeles']) {
      await inBrowserZone(zone, async () => {
        const late = patching({
          id: 'late',
          start: '2026-10-24T23:30:00',
          end: '2026-10-25T00:30:00',
        });
        const { fixture } = await mount({
          view: 'week',
          date: '2026-10-24',
          events: [late, patching()],
        });

        expect(text(all(fixture, 'a[href="/agenda/events/e1"]')[0])).toContain('9:00–11:00');
        expect(all(fixture, 'a[href="/agenda/events/late"]').map(text)[0]).toContain('23:30');
        expect(columns(fixture)[5]).toBe('sáb 24');
      });
    }
  });

  it('shows the week as one day in a narrow screen and the year in one column (§10.4)', async () => {
    const week = await mount({ view: 'week', date: '2026-10-24', narrow: true });
    const year = await mount({ view: 'year', date: '2026-01-01', narrow: true });

    expect(all(week.fixture, '[role=columnheader]')).toHaveLength(1);
    expect(text(all(week.fixture, '[role=columnheader]')[0])).toContain('sábado 24');
    expect(
      year.fixture.debugElement
        .query(By.directive(FullCalendarComponent))
        .componentInstance.getApi()
        .getOption('multiMonthMaxColumns'),
    ).toBe(1);
  });

  it('is read only: nothing can be dragged or resized (Q-18)', async () => {
    const { fixture } = await mount({ view: 'month', date: '2026-10-01', events: [patching()] });

    expect(api(fixture).getApi().getOption('editable')).toBe(false);
    expect(api(fixture).getApi().getOption('eventStartEditable')).not.toBe(true);
  });

  it('draws what a person typed as text, never as markup (CA-22)', async () => {
    const hostile = patching({
      id: 'x',
      title: '<img src=x onerror=alert(1)><script>alert(2)</script>',
    });
    const { fixture } = await mount({ view: 'month', date: '2026-10-01', events: [hostile] });

    const [event] = all(fixture, 'a[href="/agenda/events/x"]');
    expect(text(event)).toContain('<img src=x onerror=alert(1)><script>alert(2)</script>');
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(fixture.nativeElement.querySelector('script')).toBeNull();
  });

  it('names the calendar, its state legend and every link for a screen reader (RNF-10)', async () => {
    const { fixture } = await mount({ view: 'year', date: '2026-01-01', events: [patching()] });

    expect(fixture.nativeElement.querySelector('section')?.getAttribute('aria-label')).toBe(
      'Calendario del año',
    );
    const legend = fixture.nativeElement.querySelector(
      'ul[aria-label="Estados de las actividades"]',
    ) as HTMLElement;
    expect([...legend.querySelectorAll('li > span')].map(text)).toEqual([
      'Borrador',
      'Programada',
      'Completada',
      'Cancelada',
      'Vencida',
    ]);
    expect(
      [...legend.querySelectorAll('mat-icon')].every(
        (icon) => icon.getAttribute('aria-hidden') === 'true',
      ),
    ).toBe(true);
    const links = all(fixture, '[role=link]');
    expect(links.length).toBeGreaterThan(300);
    expect(
      links.every(
        (link) => link.getAttribute('aria-label') && link.getAttribute('tabindex') === '0',
      ),
    ).toBe(true);
  });
});
