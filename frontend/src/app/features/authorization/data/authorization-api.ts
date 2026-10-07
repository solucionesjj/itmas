import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { APP_CONFIG } from '../../../core/config/app-config';
import {
  AuditEvent,
  AuditFilter,
  EffectivePermissions,
  Paged,
  Permission,
  PermissionMetadata,
  PolicyDetail,
  PolicyInput,
  PolicyStatusFilter,
  PolicySummary,
  UserDetail,
  UserReference,
  UserStatusFilter,
  UserSummary,
  AssignedPolicy,
} from './authorization.models';

/** A policy together with the ETag needed to update it (optimistic concurrency). */
export interface VersionedPolicy {
  readonly policy: PolicyDetail;
  readonly etag: string;
}

/** HTTP client of the authorization administration API. Errors surface as `ApiError` (see core interceptors). */
@Injectable({ providedIn: 'root' })
export class AuthorizationApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(APP_CONFIG).apiBaseUrl + '/v1';

  searchUsers(
    search: string,
    status: UserStatusFilter,
    page: number,
    pageSize: number,
  ): Observable<Paged<UserSummary>> {
    return this.http.get<Paged<UserSummary>>(`${this.base}/users`, {
      params: params({ search, status, page, pageSize }),
    });
  }

  getUser(id: string): Observable<UserDetail> {
    return this.http.get<UserDetail>(`${this.base}/users/${id}`);
  }

  getEffectivePermissions(id: string): Observable<EffectivePermissions> {
    return this.http.get<EffectivePermissions>(`${this.base}/users/${id}/effective-permissions`);
  }

  setUserActive(id: string, isActive: boolean): Observable<void> {
    return this.http.patch<void>(`${this.base}/users/${id}/status`, { isActive });
  }

  assignPolicy(userId: string, policyId: string): Observable<void> {
    return this.http.put<void>(`${this.base}/users/${userId}/policies/${policyId}`, null);
  }

  revokePolicy(userId: string, policyId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/users/${userId}/policies/${policyId}`);
  }

  setRoot(userId: string, isRoot: boolean): Observable<void> {
    const url = `${this.base}/users/${userId}/root`;
    return isRoot ? this.http.put<void>(url, null) : this.http.delete<void>(url);
  }

  searchPolicies(
    search: string,
    status: PolicyStatusFilter,
    page: number,
    pageSize: number,
  ): Observable<Paged<PolicySummary>> {
    return this.http.get<Paged<PolicySummary>>(`${this.base}/policies`, {
      params: params({ search, status, page, pageSize }),
    });
  }

  getPolicy(id: string): Observable<VersionedPolicy> {
    return this.http
      .get<PolicyDetail>(`${this.base}/policies/${id}`, { observe: 'response' })
      .pipe(
        map((response) => ({ policy: response.body!, etag: response.headers.get('ETag') ?? '' })),
      );
  }

  getPolicyUsers(id: string): Observable<readonly UserReference[]> {
    return this.http.get<readonly UserReference[]>(`${this.base}/policies/${id}/users`);
  }

  createPolicy(input: Omit<PolicyInput, 'isActive'>): Observable<PolicyDetail> {
    return this.http.post<PolicyDetail>(`${this.base}/policies`, input);
  }

  updatePolicy(id: string, etag: string, input: PolicyInput): Observable<PolicyDetail> {
    return this.http.put<PolicyDetail>(`${this.base}/policies/${id}`, input, {
      headers: { 'If-Match': etag },
    });
  }

  deletePolicy(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/policies/${id}`);
  }

  getPermissions(): Observable<readonly Permission[]> {
    return this.http.get<readonly Permission[]>(`${this.base}/permissions`);
  }

  getPermissionPolicies(name: string): Observable<readonly AssignedPolicy[]> {
    return this.http.get<readonly AssignedPolicy[]>(
      `${this.base}/permissions/${encodeURIComponent(name)}/policies`,
    );
  }

  updatePermission(name: string, metadata: PermissionMetadata): Observable<void> {
    return this.http.patch<void>(`${this.base}/permissions/${encodeURIComponent(name)}`, metadata);
  }

  searchAuditEvents(
    filter: AuditFilter,
    page: number,
    pageSize: number,
  ): Observable<Paged<AuditEvent>> {
    return this.http.get<Paged<AuditEvent>>(`${this.base}/audit-events`, {
      params: params({ ...filter, page, pageSize }),
    });
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
