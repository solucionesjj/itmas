import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { APP_CONFIG } from '../../../core/config/app-config';
import {
  AccountItemQuery,
  AccountQuery,
  InstanceQuery,
  Paged,
  PermissionQuery,
  Sorting,
  SqlAccessItem,
  SqlAccountDetail,
  SqlAccountHistoryEntry,
  SqlAccountListItem,
  SqlInstanceDetail,
  SqlInstanceListItem,
  Validation,
} from './sql-audit.models';

/** Validation shown after unmarking: the API answers `204` and the element is pending again (RF-17). */
export const UNVALIDATED: Validation = { status: 'pending', by: null, at: null };

/**
 * HTTP client of the SQL audit API (SPEC-0005 §12.1). Errors surface as `ApiError` (see core interceptors); the
 * anti-CSRF header of the writes is added by the CSRF interceptor.
 */
@Injectable({ providedIn: 'root' })
export class SqlAuditApi {
  private readonly http = inject(HttpClient);
  private readonly base = inject(APP_CONFIG).apiBaseUrl + '/v1';

  searchInstances(query: InstanceQuery): Observable<Paged<SqlInstanceListItem>> {
    return this.http.get<Paged<SqlInstanceListItem>>(`${this.base}/sql-instances`, {
      params: params({
        search: query.search,
        engine: query.engine,
        sort: sort(query.sort),
        page: query.page,
        pageSize: query.pageSize,
      }),
    });
  }

  getInstance(id: string): Observable<SqlInstanceDetail> {
    return this.http.get<SqlInstanceDetail>(`${this.base}/sql-instances/${id}`);
  }

  searchAccounts(instanceId: string, query: AccountQuery): Observable<Paged<SqlAccountListItem>> {
    return this.http.get<Paged<SqlAccountListItem>>(
      `${this.base}/sql-instances/${instanceId}/accounts`,
      {
        params: params({
          level: query.level,
          database: query.database,
          type: query.type,
          risk: query.risk,
          validation: query.validation,
          search: query.search,
          sort: sort(query.sort),
          page: query.page,
          pageSize: query.pageSize,
        }),
      },
    );
  }

  searchPermissions(instanceId: string, query: PermissionQuery): Observable<Paged<SqlAccessItem>> {
    return this.http.get<Paged<SqlAccessItem>>(
      `${this.base}/sql-instances/${instanceId}/permissions`,
      {
        params: params({
          risk: query.risk,
          validation: query.validation,
          database: query.database,
          account: query.account,
          objectType: query.objectType,
          search: query.search,
          sort: sort(query.sort),
          page: query.page,
          pageSize: query.pageSize,
        }),
      },
    );
  }

  getAccount(id: string): Observable<SqlAccountDetail> {
    return this.http.get<SqlAccountDetail>(`${this.base}/sql-accounts/${id}`);
  }

  getAccountItems(id: string, query: AccountItemQuery): Observable<Paged<SqlAccessItem>> {
    return this.http.get<Paged<SqlAccessItem>>(`${this.base}/sql-accounts/${id}/items`, {
      params: params({
        kind: query.kind,
        risk: query.risk,
        validation: query.validation,
        database: query.database,
        objectType: query.objectType,
        page: query.page,
        pageSize: query.pageSize,
      }),
    });
  }

  getAccountChanges(
    id: string,
    page: number,
    pageSize: number,
  ): Observable<Paged<SqlAccountHistoryEntry>> {
    return this.http.get<Paged<SqlAccountHistoryEntry>>(`${this.base}/sql-accounts/${id}/changes`, {
      params: params({ page, pageSize }),
    });
  }

  /** Marks the account as valid with the fingerprint the user saw (RN-4, RN-6): `409` if it changed meanwhile. */
  validateAccount(id: string, fingerprint: string): Observable<Validation> {
    return this.http.put<Validation>(`${this.base}/sql-accounts/${id}/validation`, {
      fingerprint,
    });
  }

  unvalidateAccount(id: string): Observable<Validation> {
    return this.http
      .delete<void>(`${this.base}/sql-accounts/${id}/validation`)
      .pipe(map(() => UNVALIDATED));
  }

  /** Marks a role membership or a permission as valid: `422` if it was removed from the engine (RN-5). */
  validateItem(id: string): Observable<Validation> {
    return this.http.put<Validation>(`${this.base}/sql-access-items/${id}/validation`, null);
  }

  unvalidateItem(id: string): Observable<Validation> {
    return this.http
      .delete<void>(`${this.base}/sql-access-items/${id}/validation`)
      .pipe(map(() => UNVALIDATED));
  }
}

function sort<T extends string>(sorting: Sorting<T>): string {
  return (sorting.descending ? '-' : '') + sorting.active;
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
