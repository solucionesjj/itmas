import {
  AgendaState,
  ParamReader,
  exportTarget,
  hasFilters,
  monthGridRange,
  monthRange,
  parseAgendaState,
  periodTitle,
  shiftState,
  todayState,
  toQueryParams,
  viewState,
  visibleRange,
  weekRange,
  weekStart,
  yearRange,
} from './agenda-state';

const DEFAULT_YEAR = 2026;
const TODAY = '2026-10-18';

/** A `ParamReader` over a query string, as `ParamMap` reads the address. */
function reader(query: string): ParamReader {
  const params = new URLSearchParams(query);
  return { get: (name) => params.get(name), getAll: (name) => params.getAll(name) };
}

describe('parseAgendaState (RF-12)', () => {
  it('has a default for everything: the year list of the current year and no filters', () => {
    const state = parseAgendaState(reader(''), DEFAULT_YEAR);

    expect(state).toEqual({
      view: 'list',
      year: DEFAULT_YEAR,
      month: null,
      date: null,
      filters: { type: '', status: '', responsible: '', mine: false, tags: [], search: '' },
    });
  });

  it('reads the period and every filter from the address', () => {
    const state = parseAgendaState(
      reader(
        'year=2027&month=3&type=t1&status=overdue&responsible=u1&mine=true&tags=windows&tags=iso-27001&search=parches',
      ),
      DEFAULT_YEAR,
    );

    expect(state.year).toBe(2027);
    expect(state.month).toBe(3);
    expect(state.filters).toEqual({
      type: 't1',
      status: 'overdue',
      responsible: 'u1',
      mine: true,
      tags: ['windows', 'iso-27001'],
      search: 'parches',
    });
  });

  it('ignores a year out of range, a month out of range and a state that is not one of the closed set', () => {
    const state = parseAgendaState(
      reader('year=1800&month=13&status=hacked&mine=yes'),
      DEFAULT_YEAR,
    );

    expect(state.year).toBe(DEFAULT_YEAR);
    expect(state.month).toBeNull();
    expect(state.filters.status).toBe('');
    expect(state.filters.mine).toBe(false);
  });

  it('removes repeated tags that differ only in case and empty ones', () => {
    const state = parseAgendaState(reader('tags=Windows&tags=windows&tags='), DEFAULT_YEAR);

    expect(state.filters.tags).toEqual(['Windows']);
  });

  it('offers the four views and falls back to the year list for anything else (RF-12)', () => {
    for (const view of ['list', 'year', 'month', 'week']) {
      expect(parseAgendaState(reader(`view=${view}`), DEFAULT_YEAR).view).toBe(view);
    }
    for (const view of ['nonsense', 'MONTH', '']) {
      expect(parseAgendaState(reader(`view=${view}`), DEFAULT_YEAR).view).toBe('list');
    }
  });

  it('opens the month view on the current month of the current year and on January of another year', () => {
    const inYear = parseAgendaState(reader('view=month'), DEFAULT_YEAR, TODAY);
    const other = parseAgendaState(reader('view=month&year=2027'), DEFAULT_YEAR, TODAY);
    const chosen = parseAgendaState(reader('view=month&year=2027&month=4'), DEFAULT_YEAR, TODAY);

    expect([inYear.year, inYear.month]).toEqual([2026, 10]);
    expect([other.year, other.month]).toEqual([2027, 1]);
    expect([chosen.year, chosen.month]).toEqual([2027, 4]);
  });

  it('anchors the week view on a day, which also gives its year, and on today when the day is missing or not real', () => {
    const chosen = parseAgendaState(
      reader('view=week&date=2027-01-03&year=2030'),
      DEFAULT_YEAR,
      TODAY,
    );
    const missing = parseAgendaState(reader('view=week'), DEFAULT_YEAR, TODAY);
    const invalid = parseAgendaState(reader('view=week&date=2026-02-30'), DEFAULT_YEAR, TODAY);

    expect([chosen.date, chosen.year, chosen.month]).toEqual(['2027-01-03', 2027, null]);
    expect(missing.date).toBe(TODAY);
    expect(invalid.date).toBe(TODAY);
  });

  it('does not use a day or a month in the views that do not need them', () => {
    expect(parseAgendaState(reader('date=2026-10-24&month=3'), DEFAULT_YEAR).date).toBeNull();
    expect(parseAgendaState(reader('view=year&month=3'), DEFAULT_YEAR).month).toBeNull();
  });

  it('shows the year list when tags or a search are applied without a view (P-18)', () => {
    expect(parseAgendaState(reader('tags=windows'), DEFAULT_YEAR).view).toBe('list');
    expect(parseAgendaState(reader('search=parches'), DEFAULT_YEAR).view).toBe('list');
  });
});

describe('toQueryParams', () => {
  const base: AgendaState = parseAgendaState(reader(''), DEFAULT_YEAR);

  it('omits the defaults to keep addresses short', () => {
    expect(toQueryParams(base, DEFAULT_YEAR)).toEqual({
      view: null,
      year: null,
      month: null,
      date: null,
      type: null,
      status: null,
      responsible: null,
      mine: null,
      tags: null,
      search: null,
    });
  });

  it('writes the tags as a repeated parameter, not as a comma list', () => {
    const state = { ...base, filters: { ...base.filters, tags: ['a,b', 'c'] } };

    expect(toQueryParams(state, DEFAULT_YEAR)['tags']).toEqual(['a,b', 'c']);
  });

  it('survives the round trip through the address (CA-34)', () => {
    const state: AgendaState = {
      view: 'list',
      year: 2027,
      month: 5,
      date: null,
      filters: {
        type: 't1',
        status: 'scheduled',
        responsible: 'u1',
        mine: true,
        tags: ['windows', 'a,b'],
        search: 'parches',
      },
    };
    const written = toQueryParams(state, DEFAULT_YEAR);
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(written)) {
      for (const item of Array.isArray(value) ? value : value === null ? [] : [value]) {
        query.append(key, String(item));
      }
    }

    expect(parseAgendaState(reader(query.toString()), DEFAULT_YEAR)).toEqual(state);
  });
});

describe('the calendar views in the address (CA-34)', () => {
  const query = (state: AgendaState): string => {
    const written = toQueryParams(state, DEFAULT_YEAR);
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(written)) {
      for (const item of Array.isArray(value) ? value : value === null ? [] : [value]) {
        params.append(key, String(item));
      }
    }
    return params.toString();
  };

  it.each([
    ['year', 'view=year&year=2027'],
    ['month', 'view=month&year=2027&month=4'],
    ['week', 'view=week&date=2027-04-14'],
  ] as const)('the %s view and its period survive the round trip with the filters', (_, text) => {
    const state = parseAgendaState(
      reader(`${text}&type=t1&tags=windows&mine=true`),
      DEFAULT_YEAR,
      TODAY,
    );

    expect(parseAgendaState(reader(query(state)), DEFAULT_YEAR, TODAY)).toEqual(state);
  });

  it('writes the week by its day alone', () => {
    const state = parseAgendaState(reader('view=week&date=2027-04-14'), DEFAULT_YEAR, TODAY);

    expect(query(state)).toBe('view=week&date=2027-04-14');
  });
});

describe('the period of each view (RF-12)', () => {
  const at = (text: string) => parseAgendaState(reader(text), DEFAULT_YEAR, TODAY);

  it('knows the Monday and the range of a week', () => {
    expect(weekStart('2026-10-18')).toBe('2026-10-12');
    expect(weekStart('2026-10-19')).toBe('2026-10-19');
    expect(weekRange('2027-01-03')).toEqual({ from: '2026-12-28', to: '2027-01-04' });
  });

  it('asks for the whole weeks that the grid of a month shows', () => {
    // October 2026 starts on a Thursday and ends on a Saturday: Mon 28 Sep to Sun 1 Nov.
    expect(monthGridRange(2026, 10)).toEqual({ from: '2026-09-28', to: '2026-11-02' });
    // February 2027 starts on a Monday and ends on a Sunday: exactly four weeks.
    expect(monthGridRange(2027, 2)).toEqual({ from: '2027-02-01', to: '2027-03-01' });
    expect(monthGridRange(2026, 12)).toEqual({ from: '2026-11-30', to: '2027-01-04' });
  });

  it('asks the API for the year in the list and the year view, and for the grid or the week in the others', () => {
    expect(visibleRange(at(''))).toEqual(yearRange(2026));
    expect(visibleRange(at('view=year&year=2027'))).toEqual(yearRange(2027));
    expect(visibleRange(at('view=month&year=2026&month=10'))).toEqual(monthGridRange(2026, 10));
    expect(visibleRange(at('view=week&date=2026-10-21'))).toEqual({
      from: '2026-10-19',
      to: '2026-10-26',
    });
  });

  it('moves a year, a month, a week or, in a narrow screen, a day, wrapping at the end of the year', () => {
    expect(shiftState(at('view=year'), 1).year).toBe(2027);
    expect(shiftState(at('year=2026'), -1).year).toBe(2025);
    const december = at('view=month&year=2026&month=12');
    expect([shiftState(december, 1).year, shiftState(december, 1).month]).toEqual([2027, 1]);
    const january = at('view=month&year=2026&month=1');
    expect([shiftState(january, -1).year, shiftState(january, -1).month]).toEqual([2025, 12]);
    const week = at('view=week&date=2026-12-30');
    expect(shiftState(week, 1)).toMatchObject({ date: '2027-01-06', year: 2027 });
    expect(shiftState(week, -1)).toMatchObject({ date: '2026-12-23', year: 2026 });
    expect(shiftState(week, 1, true)).toMatchObject({ date: '2026-12-31' });
  });

  it('keeps the filters when the period changes (CA-34)', () => {
    const state = at('view=month&type=t1&tags=windows');

    expect(shiftState(state, 1).filters).toEqual(state.filters);
    expect(todayState(state, TODAY).filters).toEqual(state.filters);
    expect(viewState(state, 'week', TODAY).filters).toEqual(state.filters);
  });

  it('goes to the period that holds today', () => {
    expect(todayState(at('view=year&year=2024'), TODAY)).toMatchObject({ year: 2026, month: null });
    expect(todayState(at('view=month&year=2024&month=2'), TODAY)).toMatchObject({
      year: 2026,
      month: 10,
    });
    expect(todayState(at('view=week&date=2024-02-03'), TODAY)).toMatchObject({
      year: 2026,
      date: TODAY,
    });
  });

  it('keeps what it can when the view changes', () => {
    const july = at('year=2027&month=7');
    expect(viewState(july, 'month', TODAY)).toMatchObject({ view: 'month', year: 2027, month: 7 });
    expect(viewState(july, 'week', TODAY)).toMatchObject({
      view: 'week',
      year: 2027,
      date: '2027-07-01',
      month: null,
    });
    expect(viewState(at('year=2026&month=10'), 'week', TODAY).date).toBe(TODAY);
    expect(viewState(at('year=2026'), 'week', TODAY).date).toBe(TODAY);
    expect(viewState(at('year=2027'), 'week', TODAY).date).toBe('2027-01-01');
    expect(viewState(at('view=week&date=2026-12-30'), 'month', TODAY)).toMatchObject({
      year: 2026,
      month: 12,
      date: null,
    });
    expect(viewState(at('view=month&year=2026&month=3'), 'year', TODAY)).toMatchObject({
      view: 'year',
      month: null,
    });
    // The year list always opens on the whole year: the month of the month view is not left behind as a filter.
    expect(viewState(at('view=month&year=2026&month=10'), 'list', TODAY)).toMatchObject({
      view: 'list',
      year: 2026,
      month: null,
    });
    expect(viewState(at('view=week&date=2026-12-30'), 'list', TODAY)).toMatchObject({
      view: 'list',
      date: null,
      month: null,
    });
  });

  it('titles the period', () => {
    expect(periodTitle(at('year=2027'))).toBe('2027');
    expect(periodTitle(at('view=month&year=2026&month=10'))).toBe('Octubre de 2026');
    expect(periodTitle(at('view=week&date=2026-10-21'))).toBe('19 oct – 25 oct 2026');
    expect(periodTitle(at('view=week&date=2027-01-01'))).toBe('28 dic 2026 – 3 ene 2027');
    expect(periodTitle(at('view=week&date=2026-10-24'), true)).toBe('sáb 24 oct 2026');
  });
});

describe('hasFilters', () => {
  const none = parseAgendaState(reader(''), DEFAULT_YEAR).filters;

  it('is false without filters and true with any of them', () => {
    expect(hasFilters(none)).toBe(false);
    expect(hasFilters({ ...none, type: 't1' })).toBe(true);
    expect(hasFilters({ ...none, status: 'draft' })).toBe(true);
    expect(hasFilters({ ...none, responsible: 'u1' })).toBe(true);
    expect(hasFilters({ ...none, mine: true })).toBe(true);
    expect(hasFilters({ ...none, tags: ['x'] })).toBe(true);
    expect(hasFilters({ ...none, search: 'x' })).toBe(true);
  });
});

describe('yearRange', () => {
  it('goes from the first day of the year to the first day of the next one (exclusive)', () => {
    expect(yearRange(2026)).toEqual({ from: '2026-01-01', to: '2027-01-01' });
  });
});

describe('monthRange', () => {
  it('goes from the first day of the month to the first day of the next one (exclusive)', () => {
    expect(monthRange(2026, 3)).toEqual({ from: '2026-03-01', to: '2026-04-01' });
    expect(monthRange(2026, 10)).toEqual({ from: '2026-10-01', to: '2026-11-01' });
  });

  it('rolls over to January of the next year after December', () => {
    expect(monthRange(2026, 12)).toEqual({ from: '2026-12-01', to: '2027-01-01' });
  });

  it('covers February of a leap year and of a common one', () => {
    expect(monthRange(2028, 2)).toEqual({ from: '2028-02-01', to: '2028-03-01' });
    expect(monthRange(2027, 2)).toEqual({ from: '2027-02-01', to: '2027-03-01' });
  });
});

describe('exportTarget (RF-21)', () => {
  it('covers the whole year when the list is not narrowed to a month', () => {
    expect(exportTarget({ year: 2026, month: null })).toEqual({
      from: '2026-01-01',
      to: '2027-01-01',
      fileName: 'agenda-2026.ics',
    });
  });

  it('covers the month the list is narrowed to, and names the file after it', () => {
    expect(exportTarget({ year: 2026, month: 3 })).toEqual({
      from: '2026-03-01',
      to: '2026-04-01',
      fileName: 'agenda-2026-03.ics',
    });
    expect(exportTarget({ year: 2026, month: 12 })).toEqual({
      from: '2026-12-01',
      to: '2027-01-01',
      fileName: 'agenda-2026-12.ics',
    });
  });

  it('never covers more than the 366 days the API accepts', () => {
    const days = (target: { from: string; to: string }) =>
      (Date.parse(target.to) - Date.parse(target.from)) / 86_400_000;

    expect(days(exportTarget({ year: 2028, month: null }))).toBe(366);
    expect(days(exportTarget({ year: 2026, month: null }))).toBe(365);
  });
});
