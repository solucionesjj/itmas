import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Alert, AlertStatus, AlertsQuery, PagedResult } from './alert.model';

@Injectable({ providedIn: 'root' })
export class AlertsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/alerts`;

  list(query: AlertsQuery): Observable<PagedResult<Alert>> {
    let params = new HttpParams();
    if (query.type) {
      params = params.set('type', query.type);
    }
    if (query.status) {
      params = params.set('status', query.status);
    }
    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    params = params.set('page', query.page ?? 1);
    params = params.set('limit', query.limit ?? 20);

    return this.http.get<PagedResult<Alert>>(this.baseUrl, { params });
  }

  updateStatus(id: string, status: AlertStatus): Observable<Alert> {
    return this.http.patch<Alert>(`${this.baseUrl}/${id}`, { status });
  }
}
