import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  SacExportFormat,
  SacStatisticsPage,
  SacStatisticsQuery
} from './sac-statistic.model';

@Injectable({ providedIn: 'root' })
export class SacStatisticsService {
  private readonly http = inject(HttpClient);

  list(query: SacStatisticsQuery): Observable<SacStatisticsPage> {
    return this.http.get<SacStatisticsPage>(`${environment.apiBaseUrl}/sac-statistics`, {
      params: toParams({ ...query })
    });
  }

  /**
   * The export is a file download, so it goes through the shared
   * `/reports/export` endpoint rather than a second one on this resource.
   * `observe: 'response'` keeps the `Content-Disposition` header, which carries
   * the server's own filename.
   */
  export(
    format: SacExportFormat,
    query: Pick<SacStatisticsQuery, 'databaseName' | 'from' | 'to'>
  ): Observable<HttpResponse<Blob>> {
    return this.http.get(`${environment.apiBaseUrl}/reports/export`, {
      params: toParams({ ...query, reportType: 'sac-statistics', format }),
      responseType: 'blob',
      observe: 'response'
    });
  }
}

/**
 * Only set params, so a cleared filter leaves the query string entirely instead
 * of arriving as `databaseName=` — which the server would treat as a filter for
 * the empty string.
 */
function toParams(query: Record<string, string | number | undefined>): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(query)) {
    if (value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}
