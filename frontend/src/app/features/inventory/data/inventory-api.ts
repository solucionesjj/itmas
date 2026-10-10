import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { APP_CONFIG } from '../../../core/config/app-config';
import {
  DeviceCreated,
  DeviceDetail,
  DeviceInput,
  DeviceListItem,
  DeviceReport,
  DeviceReportListItem,
  DeviceSort,
  DeviceStatusFilter,
  DeviceType,
  EnrollmentCode,
  Paged,
} from './inventory.models';

export interface DeviceQuery {
  readonly search: string;
  readonly status: DeviceStatusFilter;
  readonly type: DeviceType | '';
  readonly sort: DeviceSort;
  readonly descending: boolean;
  readonly page: number;
  readonly pageSize: number;
}

/** A device together with the ETag needed to update it (optimistic concurrency). */
export interface VersionedDevice {
  readonly device: DeviceDetail;
  readonly etag: string;
}

/**
 * HTTP client of the device inventory API (SPEC-0003). Errors surface as `ApiError` (see core interceptors).
 * Enrollment codes are returned to the caller and never kept here.
 */
@Injectable({ providedIn: 'root' })
export class InventoryApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(APP_CONFIG).apiBaseUrl + '/v1/devices';

  searchDevices(query: DeviceQuery): Observable<Paged<DeviceListItem>> {
    const sort = (query.descending ? '-' : '') + query.sort;
    return this.http.get<Paged<DeviceListItem>>(this.base, {
      params: params({
        search: query.search,
        status: query.status,
        type: query.type,
        sort,
        page: query.page,
        pageSize: query.pageSize,
      }),
    });
  }

  getDevice(id: string): Observable<VersionedDevice> {
    return this.http
      .get<DeviceDetail>(`${this.base}/${id}`, { observe: 'response' })
      .pipe(
        map((response) => ({ device: response.body!, etag: response.headers.get('ETag') ?? '' })),
      );
  }

  createDevice(input: DeviceInput): Observable<DeviceCreated> {
    return this.http.post<DeviceCreated>(this.base, input);
  }

  updateDevice(id: string, etag: string, input: DeviceInput): Observable<DeviceDetail> {
    return this.http.put<DeviceDetail>(`${this.base}/${id}`, input, {
      headers: { 'If-Match': etag },
    });
  }

  regenerateEnrollmentCode(id: string): Observable<EnrollmentCode> {
    return this.http.post<EnrollmentCode>(`${this.base}/${id}/enrollment-code`, null);
  }

  deleteDevice(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  getReports(id: string, page: number, pageSize: number): Observable<Paged<DeviceReportListItem>> {
    return this.http.get<Paged<DeviceReportListItem>>(`${this.base}/${id}/reports`, {
      params: params({ page, pageSize }),
    });
  }

  /** The latest report when `reportId` is omitted. */
  getReport(id: string, reportId?: string): Observable<DeviceReport> {
    return this.http.get<DeviceReport>(`${this.base}/${id}/reports/${reportId ?? 'latest'}`);
  }
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
