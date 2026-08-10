import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AwsSyncRun, AwsSyncSummary } from './security-group-sync.model';

@Injectable({ providedIn: 'root' })
export class SecurityGroupSyncService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/security-group-sync`;

  run(): Observable<AwsSyncRun> {
    return this.http.post<AwsSyncRun>(`${this.baseUrl}/run`, {});
  }

  listRuns(limit = 20): Observable<AwsSyncRun[]> {
    const params = new HttpParams().set('limit', limit);
    return this.http.get<AwsSyncRun[]>(`${this.baseUrl}/runs`, { params });
  }

  summary(): Observable<AwsSyncSummary> {
    return this.http.get<AwsSyncSummary>(`${this.baseUrl}/summary`);
  }
}
