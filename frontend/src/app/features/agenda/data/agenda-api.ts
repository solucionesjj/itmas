import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { APP_CONFIG } from '../../../core/config/app-config';
import {
  CompletionInput,
  EventDetail,
  EventInput,
  EventList,
  EventType,
  EventTypeInput,
  OutlookState,
  PersonOption,
  ScheduleResult,
  SeriesCommonInput,
  SeriesCreated,
  SeriesInput,
  SeriesOccurrence,
  StatusFilter,
  Summary,
  TagCount,
  VersionedEvent,
} from './agenda.models';

/** Filters shared by every view of the agenda (RF-12, RF-25). Tags require the event to have all of them. */
export interface AgendaFilters {
  readonly type: string;
  readonly status: StatusFilter | '';
  readonly responsible: string;
  readonly mine: boolean;
  readonly tags: readonly string[];
  readonly search: string;
}

export const NO_FILTERS: AgendaFilters = {
  type: '',
  status: '',
  responsible: '',
  mine: false,
  tags: [],
  search: '',
};

/**
 * HTTP client of the infrastructure agenda API (SPEC-0006 §12.1). Errors surface as `ApiError` (see core interceptors).
 * Updates carry the `If-Match` of the version the person read.
 */
@Injectable({ providedIn: 'root' })
export class AgendaApi {
  private readonly http = inject(HttpClient);
  private readonly root = inject(APP_CONFIG).apiBaseUrl + '/v1';
  private readonly events = `${this.root}/agenda-events`;

  /** Events that share time with `from` (inclusive) to `to` (exclusive), local dates `yyyy-MM-dd`. */
  list(from: string, to: string, filters: AgendaFilters): Observable<EventList> {
    return this.http.get<EventList>(this.events, {
      params: filterParams(filters, { from, to }),
    });
  }

  /**
   * The scheduled activities of `from` (inclusive) to `to` (exclusive) as an iCalendar file (RF-21). The filters are the
   * list's, except the state: the file only ever has Scheduled activities, so the API does not take it. The answer is
   * a `Blob`, never parsed: it is handed to the browser as a download.
   */
  exportCalendar(from: string, to: string, filters: AgendaFilters): Observable<Blob> {
    return this.http.get(`${this.events}/export.ics`, {
      params: filterParams({ ...filters, status: '' }, { from, to }),
      responseType: 'blob',
    });
  }

  summary(year: number, filters: AgendaFilters): Observable<Summary> {
    return this.http.get<Summary>(`${this.events}/summary`, {
      params: filterParams({ ...filters, status: '' }, { year }),
    });
  }

  getEvent(id: string): Observable<VersionedEvent> {
    return this.http
      .get<EventDetail>(`${this.events}/${segment(id)}`, { observe: 'response' })
      .pipe(map((response) => versioned(response.body!, response.headers.get('ETag'))));
  }

  createEvent(input: EventInput): Observable<VersionedEvent> {
    return this.http
      .post<EventDetail>(this.events, input, { observe: 'response' })
      .pipe(map((response) => versioned(response.body!, response.headers.get('ETag'))));
  }

  updateEvent(id: string, etag: string, input: EventInput): Observable<VersionedEvent> {
    return this.http
      .put<EventDetail>(`${this.events}/${segment(id)}`, input, {
        headers: { 'If-Match': etag },
        observe: 'response',
      })
      .pipe(map((response) => versioned(response.body!, response.headers.get('ETag'))));
  }

  deleteEvent(id: string): Observable<void> {
    return this.http.delete<void>(`${this.events}/${segment(id)}`);
  }

  /** Replaces the tags in any status; it changes neither the ETag nor Outlook (RF-24). */
  setTags(id: string, tags: readonly string[]): Observable<readonly string[]> {
    return this.http
      .put<{ tags: readonly string[] }>(`${this.events}/${segment(id)}/tags`, { tags })
      .pipe(map((response) => response.tags));
  }

  listTags(search: string, limit = 20): Observable<readonly TagCount[]> {
    return this.http
      .get<{ items: readonly TagCount[] }>(`${this.root}/agenda-tags`, {
        params: params({ search, limit }),
      })
      .pipe(map((response) => response.items));
  }

  /** People that can be the responsible; without a search the caller is always included (P-31). */
  listPeople(search = ''): Observable<readonly PersonOption[]> {
    return this.http
      .get<{ items: readonly PersonOption[] }>(`${this.root}/agenda-people`, {
        params: params({ search }),
      })
      .pipe(map((response) => response.items));
  }

  /** Schedules up to 50 drafts; each one has its own result (RF-9). */
  schedule(ids: readonly string[]): Observable<readonly ScheduleResult[]> {
    return this.http
      .post<{ results: readonly ScheduleResult[] }>(`${this.events}/schedule`, { ids })
      .pipe(map((response) => response.results));
  }

  cancel(id: string, reason: string): Observable<VersionedEvent> {
    return this.http
      .post<EventDetail>(
        `${this.events}/${segment(id)}/cancel`,
        { reason },
        { observe: 'response' },
      )
      .pipe(map((response) => versioned(response.body!, response.headers.get('ETag'))));
  }

  complete(id: string, input: CompletionInput): Observable<VersionedEvent> {
    return this.http
      .put<EventDetail>(`${this.events}/${segment(id)}/completion`, input, {
        observe: 'response',
      })
      .pipe(map((response) => versioned(response.body!, response.headers.get('ETag'))));
  }

  syncOutlook(id: string): Observable<OutlookState> {
    return this.http
      .post<{ outlook: OutlookState }>(`${this.events}/${segment(id)}/outlook-sync`, null)
      .pipe(map((response) => response.outlook));
  }

  syncAllOutlook(): Observable<readonly { id: string; outlook: OutlookState }[]> {
    return this.http
      .post<{ results: readonly { id: string; outlook: OutlookState }[] }>(
        `${this.events}/outlook-sync`,
        null,
      )
      .pipe(map((response) => response.results));
  }

  /** The dates and warnings a series would have, without saving anything (RF-4). */
  previewSeries(input: SeriesInput): Observable<readonly SeriesOccurrence[]> {
    return this.http
      .post<{ occurrences: readonly SeriesOccurrence[] }>(`${this.root}/agenda-series`, input, {
        params: { dryRun: 'true' },
      })
      .pipe(map((response) => response.occurrences));
  }

  createSeries(input: SeriesInput): Observable<SeriesCreated> {
    return this.http.post<SeriesCreated>(`${this.root}/agenda-series`, input);
  }

  updateSeries(id: string, input: SeriesCommonInput): Observable<{ updated: number }> {
    return this.http.put<{ updated: number }>(`${this.root}/agenda-series/${segment(id)}`, input);
  }

  listTypes(): Observable<readonly EventType[]> {
    return this.http
      .get<{ items: readonly EventType[] }>(`${this.root}/agenda-event-types`)
      .pipe(map((response) => response.items));
  }

  createType(input: EventTypeInput): Observable<EventType> {
    return this.http.post<EventType>(`${this.root}/agenda-event-types`, input);
  }

  updateType(id: string, etag: string, input: EventTypeInput): Observable<EventType> {
    return this.http.put<EventType>(`${this.root}/agenda-event-types/${segment(id)}`, input, {
      headers: { 'If-Match': etag },
    });
  }
}

/** The `ETag` the API gives a revision: the revision number between quotes (`"3"`). The type list carries no header, so it is rebuilt. */
export const etagOf = (revision: number): string => `"${revision}"`;

function versioned(event: EventDetail, etag: string | null): VersionedEvent {
  return { event, etag: etag ?? etagOf(event.revision) };
}

/** Query string of the filters: repeated `tags` (never a comma list, so any character is allowed). */
function filterParams(filters: AgendaFilters, extra: Record<string, string | number>): HttpParams {
  let result = params({
    ...extra,
    type: filters.type,
    status: filters.status,
    responsible: filters.responsible,
    search: filters.search,
  });
  if (filters.mine) {
    result = result.set('mine', 'true');
  }
  for (const tag of filters.tags) {
    result = result.append('tags', tag);
  }
  return result;
}

/**
 * An id taken from the address as one segment of a path: encoded, and never a dot segment (`.` and `..`, which the browser resolves
 * before sending), so a crafted link cannot make a request reach another route.
 */
function segment(id: string): string {
  return encodeURIComponent(id).replace(/^\.+$/, (dots) => '%2E'.repeat(dots.length));
}

function params(values: Record<string, string | number | undefined | null>): HttpParams {
  let result = new HttpParams();
  for (const [key, value] of Object.entries(values)) {
    if (value !== undefined && value !== null && value !== '') {
      result = result.set(key, String(value));
    }
  }
  return result;
}
