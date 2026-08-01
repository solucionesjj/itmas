import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { DeviceStats, OsStat } from './stats.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);

  getDeviceStats(): Observable<DeviceStats> {
    return this.http.get<DeviceStats>(`${environment.apiBaseUrl}/stats/devices`);
  }

  getOsStats(): Observable<OsStat[]> {
    return this.http.get<OsStat[]>(`${environment.apiBaseUrl}/stats/os`);
  }
}
