import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { AgendaApi, NO_FILTERS, etagOf } from './agenda-api';
import { detailOf } from './agenda-fixtures';
import { EventInput, SeriesInput } from './agenda.models';

describe('AgendaApi (SPEC-0006 §12.1)', () => {
  let api: AgendaApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(AgendaApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const eventInput: EventInput = {
    typeId: 't1',
    title: 'Parches',
    description: null,
    responsibleUserId: 'u1',
    participants: [],
    notifyTeam: true,
    impact: 'none',
    scope: null,
    location: null,
    reminderMinutes: null,
    tags: [],
    isAllDay: false,
    start: '2026-10-24T09:00',
    end: null,
  };

  it('lists the events of a range with every filter, and the tags as a repeated parameter (P-18)', () => {
    api
      .list('2026-01-01', '2027-01-01', {
        type: 't1',
        status: 'overdue',
        responsible: 'u1',
        mine: true,
        tags: ['windows', 'a,b'],
        search: 'parches',
      })
      .subscribe();

    const request = http.expectOne((r) => r.url === '/api/v1/agenda-events');
    expect(request.request.method).toBe('GET');
    const params = request.request.params;
    expect(params.get('from')).toBe('2026-01-01');
    expect(params.get('to')).toBe('2027-01-01');
    expect(params.get('type')).toBe('t1');
    expect(params.get('status')).toBe('overdue');
    expect(params.get('responsible')).toBe('u1');
    expect(params.get('mine')).toBe('true');
    expect(params.get('search')).toBe('parches');
    expect(params.getAll('tags')).toEqual(['windows', 'a,b']);
    request.flush({
      items: [],
      truncated: false,
      timeZone: 'America/Bogota',
      outlookEnabled: false,
    });
  });

  it('sends no parameter for an empty filter', () => {
    api.list('2026-01-01', '2027-01-01', NO_FILTERS).subscribe();

    const request = http.expectOne((r) => r.url === '/api/v1/agenda-events');
    expect(request.request.params.keys().sort()).toEqual(['from', 'to']);
    request.flush({
      items: [],
      truncated: false,
      timeZone: 'America/Bogota',
      outlookEnabled: false,
    });
  });

  it('asks for the summary of a year without the state filter (the summary already splits by state)', () => {
    api.summary(2026, { ...NO_FILTERS, status: 'draft', tags: ['windows'] }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/v1/agenda-events/summary');
    expect(request.request.params.get('year')).toBe('2026');
    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.getAll('tags')).toEqual(['windows']);
    request.flush({ year: 2026, months: [], byType: [] });
  });

  describe('exportCalendar (RF-21)', () => {
    it('downloads the file as a blob from the export route with the range and every filter but the state', async () => {
      const file = new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' });
      const result = firstValueFrom(
        api.exportCalendar('2026-01-01', '2027-01-01', {
          type: 't1',
          status: 'draft',
          responsible: 'u1',
          mine: true,
          tags: ['windows', 'a,b'],
          search: 'parches',
        }),
      );

      const request = http.expectOne((r) => r.url === '/api/v1/agenda-events/export.ics');
      expect(request.request.method).toBe('GET');
      expect(request.request.responseType).toBe('blob');
      const params = request.request.params;
      expect(params.get('from')).toBe('2026-01-01');
      expect(params.get('to')).toBe('2027-01-01');
      expect(params.get('type')).toBe('t1');
      expect(params.get('responsible')).toBe('u1');
      expect(params.get('mine')).toBe('true');
      expect(params.get('search')).toBe('parches');
      expect(params.getAll('tags')).toEqual(['windows', 'a,b']);
      // The file only has Scheduled activities: the state of the list is not sent.
      expect(params.has('status')).toBe(false);
      request.flush(file);

      expect(await result).toBe(file);
    });

    it('sends only the range when there are no filters', () => {
      api.exportCalendar('2026-03-01', '2026-04-01', NO_FILTERS).subscribe();

      const request = http.expectOne((r) => r.url === '/api/v1/agenda-events/export.ics');
      expect(request.request.params.keys().sort()).toEqual(['from', 'to']);
      request.flush(new Blob([]));
    });
  });

  it('encodes the id of the address before it goes into a path, so it cannot reach another route', () => {
    const hostile = '..%2F..%2Fsql-instances';

    api.getEvent(hostile).subscribe();
    api.deleteEvent('a/b').subscribe();
    api.setTags('a?b#c', []).subscribe();
    api.syncOutlook('..').subscribe();

    http
      .expectOne({ method: 'GET', url: '/api/v1/agenda-events/..%252F..%252Fsql-instances' })
      .flush(detailOf());
    http.expectOne({ method: 'DELETE', url: '/api/v1/agenda-events/a%2Fb' }).flush(null);
    http
      .expectOne({ method: 'PUT', url: '/api/v1/agenda-events/a%3Fb%23c/tags' })
      .flush({ tags: [] });
    http
      .expectOne({ method: 'POST', url: '/api/v1/agenda-events/%2E%2E/outlook-sync' })
      .flush({ outlook: { status: 'published' } });
  });

  it('reads the ETag of an event so the update can carry it (RNF-3)', async () => {
    const result = firstValueFrom(api.getEvent('e1'));

    http
      .expectOne({ method: 'GET', url: '/api/v1/agenda-events/e1' })
      .flush(detailOf({ revision: 7 }), { headers: { ETag: '"7"' } });

    expect((await result).etag).toBe('"7"');
  });

  it('rebuilds the ETag from the revision when the header is missing', async () => {
    const result = firstValueFrom(api.getEvent('e1'));

    http.expectOne('/api/v1/agenda-events/e1').flush(detailOf({ revision: 4 }));

    expect((await result).etag).toBe(etagOf(4));
    expect(etagOf(4)).toBe('"4"');
  });

  it('updates an event with If-Match and creates it without it', () => {
    api.updateEvent('e1', '"3"', eventInput).subscribe();
    api.createEvent(eventInput).subscribe();

    const update = http.expectOne({ method: 'PUT', url: '/api/v1/agenda-events/e1' });
    expect(update.request.headers.get('If-Match')).toBe('"3"');
    expect(update.request.body).toEqual(eventInput);
    update.flush(detailOf());
    const create = http.expectOne({ method: 'POST', url: '/api/v1/agenda-events' });
    expect(create.request.headers.has('If-Match')).toBe(false);
    create.flush(detailOf());
  });

  it('replaces the tags with the list given and answers the saved ones (RF-24)', async () => {
    const result = firstValueFrom(api.setTags('e1', ['Windows', 'producción']));

    const request = http.expectOne({ method: 'PUT', url: '/api/v1/agenda-events/e1/tags' });
    expect(request.request.body).toEqual({ tags: ['Windows', 'producción'] });
    expect(request.request.headers.has('If-Match')).toBe(false);
    request.flush({ tags: ['Windows', 'producción'] });

    expect(await result).toEqual(['Windows', 'producción']);
  });

  it('suggests tags and people with a search and a limit', () => {
    api.listTags('win', 10).subscribe();
    api.listPeople('ana').subscribe();

    const tags = http.expectOne((r) => r.url === '/api/v1/agenda-tags');
    expect(tags.request.params.toString()).toBe('search=win&limit=10');
    tags.flush({ items: [{ name: 'windows', count: 3 }] });
    const people = http.expectOne((r) => r.url === '/api/v1/agenda-people');
    expect(people.request.params.toString()).toBe('search=ana');
    people.flush({ items: [] });
  });

  it('schedules a batch, cancels with a reason and records the execution', () => {
    api.schedule(['e1', 'e2']).subscribe();
    api.cancel('e1', 'Cambio de ventana').subscribe();
    api
      .complete('e1', { result: 'succeeded', executedAt: null, notes: null, evidenceLinks: [] })
      .subscribe();

    const schedule = http.expectOne({ method: 'POST', url: '/api/v1/agenda-events/schedule' });
    expect(schedule.request.body).toEqual({ ids: ['e1', 'e2'] });
    schedule.flush({ results: [] });
    const cancel = http.expectOne({ method: 'POST', url: '/api/v1/agenda-events/e1/cancel' });
    expect(cancel.request.body).toEqual({ reason: 'Cambio de ventana' });
    cancel.flush(detailOf());
    const complete = http.expectOne({ method: 'PUT', url: '/api/v1/agenda-events/e1/completion' });
    expect(complete.request.body).toMatchObject({ result: 'succeeded' });
    complete.flush(detailOf());
  });

  it('synchronizes one event and all the pending ones with Outlook (RF-19)', async () => {
    const one = firstValueFrom(api.syncOutlook('e1'));
    http
      .expectOne({ method: 'POST', url: '/api/v1/agenda-events/e1/outlook-sync' })
      .flush({ outlook: { status: 'published' } });
    expect(await one).toEqual({ status: 'published' });

    const all = firstValueFrom(api.syncAllOutlook());
    http
      .expectOne({ method: 'POST', url: '/api/v1/agenda-events/outlook-sync' })
      .flush({ results: [{ id: 'e1', outlook: { status: 'pending' } }] });
    expect(await all).toEqual([{ id: 'e1', outlook: { status: 'pending' } }]);
  });

  it('previews a series with dryRun and creates it without it (RF-4)', async () => {
    const input: SeriesInput = {
      event: eventInput,
      rule: { unit: 'month', interval: 1, anchor: null, endsOn: null, count: 12 },
    };
    const preview = firstValueFrom(api.previewSeries(input));
    const dry = http.expectOne((r) => r.url === '/api/v1/agenda-series');
    expect(dry.request.method).toBe('POST');
    expect(dry.request.params.get('dryRun')).toBe('true');
    expect(dry.request.body).toEqual(input);
    dry.flush({
      occurrences: [
        { start: '2026-10-24T09:00:00', end: '2026-10-24T10:00:00', isAllDay: false, warnings: [] },
      ],
    });
    expect((await preview).length).toBe(1);

    api.createSeries(input).subscribe();
    const real = http.expectOne((r) => r.url === '/api/v1/agenda-series');
    expect(real.request.params.has('dryRun')).toBe(false);
    real.flush({ seriesId: 's1', eventIds: ['e1'] });
  });

  it('updates the common fields of a series', () => {
    api
      .updateSeries('s1', {
        typeId: 't1',
        title: 'Parches',
        description: null,
        responsibleUserId: 'u1',
        participants: [],
        impact: 'none',
        scope: null,
        location: null,
        reminderMinutes: null,
        tags: ['windows'],
      })
      .subscribe();

    const request = http.expectOne({ method: 'PUT', url: '/api/v1/agenda-series/s1' });
    expect(request.request.body.tags).toEqual(['windows']);
    request.flush({ updated: 5 });
  });

  it('creates a type, and updates it with If-Match', () => {
    const type = {
      name: 'Hardening',
      description: null,
      defaultDurationMinutes: 60,
      defaultReminderMinutes: 1440,
      isFreeze: false,
    };
    api.createType(type).subscribe();
    api.updateType('t1', '"2"', { ...type, isActive: false }).subscribe();

    http.expectOne({ method: 'POST', url: '/api/v1/agenda-event-types' }).flush({});
    const update = http.expectOne({ method: 'PUT', url: '/api/v1/agenda-event-types/t1' });
    expect(update.request.headers.get('If-Match')).toBe('"2"');
    expect(update.request.body.isActive).toBe(false);
    update.flush({});
  });
});
