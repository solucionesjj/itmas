import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Device, DevicesQuery, PagedResult } from './device.model';

@Injectable({ providedIn: 'root' })
export class DevicesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/devices`;

  list(query: DevicesQuery): Observable<PagedResult<Device>> {
    let params = new HttpParams();
    if (query.category) {
      params = params.set('category', query.category);
    }
    if (query.osName) {
      params = params.set('osName', query.osName);
    }
    if (query.hostname) {
      params = params.set('hostname', query.hostname);
    }
    params = params.set('page', query.page ?? 1);
    params = params.set('limit', query.limit ?? 20);

    return this.http.get<PagedResult<Device>>(this.baseUrl, { params });
  }
}
