import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  DistinctGroup,
  PagedResult,
  SecurityGroupRule,
  SecurityGroupRulesExportQuery,
  SecurityGroupRulesQuery
} from './security-group-rule.model';

@Injectable({ providedIn: 'root' })
export class SecurityGroupRulesService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/security-group-rules`;

  list(query: SecurityGroupRulesQuery): Observable<PagedResult<SecurityGroupRule>> {
    let params = this.filterParams(query);
    if (query.sortBy) {
      params = params.set('sortBy', query.sortBy);
    }
    if (query.sortDir) {
      params = params.set('sortDir', query.sortDir);
    }
    params = params.set('page', query.page ?? 1);
    params = params.set('limit', query.limit ?? 20);

    return this.http.get<PagedResult<SecurityGroupRule>>(this.baseUrl, { params });
  }

  listGroups(): Observable<DistinctGroup[]> {
    return this.http.get<DistinctGroup[]>(`${this.baseUrl}/groups`);
  }

  review(id: string, observation: string): Observable<SecurityGroupRule> {
    return this.http.patch<SecurityGroupRule>(`${this.baseUrl}/${id}/review`, {
      observation
    });
  }

  authorize(id: string, observation: string): Observable<SecurityGroupRule> {
    return this.http.patch<SecurityGroupRule>(
      `${this.baseUrl}/${id}/authorize`,
      { observation }
    );
  }

  export(query: SecurityGroupRulesExportQuery): Observable<HttpResponse<Blob>> {
    const params = this.filterParams(query).set('format', query.format);
    return this.http.get(`${this.baseUrl}/export`, {
      params,
      responseType: 'blob',
      observe: 'response'
    });
  }

  private filterParams(
    query: Omit<SecurityGroupRulesQuery, 'sortBy' | 'sortDir' | 'page' | 'limit'>
  ): HttpParams {
    let params = new HttpParams();
    if (query.q) params = params.set('q', query.q);
    if (query.securityGroupId) {
      params = params.set('securityGroupId', query.securityGroupId);
    }
    if (query.status) params = params.set('status', query.status);
    if (query.createdFrom) params = params.set('createdFrom', query.createdFrom);
    if (query.createdTo) params = params.set('createdTo', query.createdTo);
    if (query.reviewedFrom) params = params.set('reviewedFrom', query.reviewedFrom);
    if (query.reviewedTo) params = params.set('reviewedTo', query.reviewedTo);
    if (query.authorizedFrom) {
      params = params.set('authorizedFrom', query.authorizedFrom);
    }
    if (query.authorizedTo) params = params.set('authorizedTo', query.authorizedTo);
    return params;
  }
}
