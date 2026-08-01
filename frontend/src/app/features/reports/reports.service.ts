import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ReportQuery } from './report.model';

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/reports/export`;

  download(query: ReportQuery): Observable<HttpResponse<Blob>> {
    // Wire param is `reportType`, not `type` — the backend reserves `type` for
    // the alert domain's own field (resource_change|off_hours_access) and
    // would reject an unrecognized `type` under its strict whitelist validation.
    let params = new HttpParams().set('reportType', query.type).set('format', query.format);

    if (query.type === 'devices') {
      if (query.category) {
        params = params.set('category', query.category);
      }
      if (query.osName) {
        params = params.set('osName', query.osName);
      }
      if (query.hostname) {
        params = params.set('hostname', query.hostname);
      }
    } else {
      if (query.status) {
        params = params.set('status', query.status);
      }
      if (query.from) {
        params = params.set('from', query.from);
      }
      if (query.to) {
        params = params.set('to', query.to);
      }
    }

    return this.http.get(this.baseUrl, {
      params,
      responseType: 'blob',
      observe: 'response'
    });
  }
}
