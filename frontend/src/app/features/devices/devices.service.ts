import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  CreateDeviceRequest,
  CreateDeviceResponse,
  Device,
  DevicesQuery,
  PagedResult,
  RotateDeviceKeyResponse
} from './device.model';

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

  // Administrador only (ADR-0016) — backend enforces RBAC; this is just the
  // HTTP call. apiKey in the response is shown once by the caller and never
  // requested again.
  create(request: CreateDeviceRequest): Observable<CreateDeviceResponse> {
    return this.http.post<CreateDeviceResponse>(this.baseUrl, request);
  }

  rotateKey(deviceId: string): Observable<RotateDeviceKeyResponse> {
    return this.http.post<RotateDeviceKeyResponse>(
      `${this.baseUrl}/${deviceId}/rotate-key`,
      {}
    );
  }
}
