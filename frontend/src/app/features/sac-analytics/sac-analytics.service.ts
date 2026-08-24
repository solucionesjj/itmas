import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SacGrowth, SacMetric, SacRanking } from './sac-analytics.model';

@Injectable({ providedIn: 'root' })
export class SacAnalyticsService {
  private readonly http = inject(HttpClient);

  ranking(metric: SacMetric, at?: string): Observable<SacRanking> {
    let params = new HttpParams().set('metric', metric);
    if (at) {
      params = params.set('at', at);
    }
    return this.http.get<SacRanking>(`${environment.apiBaseUrl}/stats/sac/ranking`, {
      params
    });
  }

  growth(metric: SacMetric, months: number, databaseName?: string): Observable<SacGrowth> {
    let params = new HttpParams().set('metric', metric).set('months', String(months));
    if (databaseName) {
      params = params.set('databaseName', databaseName);
    }
    return this.http.get<SacGrowth>(`${environment.apiBaseUrl}/stats/sac/growth`, {
      params
    });
  }

  databases(): Observable<string[]> {
    return this.http.get<string[]>(`${environment.apiBaseUrl}/stats/sac/databases`);
  }
}
