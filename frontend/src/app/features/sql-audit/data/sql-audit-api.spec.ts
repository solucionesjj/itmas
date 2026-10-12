import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { SqlAuditApi } from './sql-audit-api';
import { pageOf } from './sql-audit-fixtures';
import { Validation } from './sql-audit.models';

describe('SqlAuditApi', () => {
  let api: SqlAuditApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(SqlAuditApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  const valid: Validation = { status: 'valid', by: 'Ana', at: '2026-10-11T10:00:00Z' };

  it('searches instances with the filters set, the sort and the page', () => {
    api
      .searchInstances({
        search: 'ventas',
        engine: '',
        sort: { active: 'pendingCritical', descending: true },
        page: 2,
        pageSize: 50,
      })
      .subscribe();

    const request = http.expectOne((r) => r.url === '/api/v1/sql-instances');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.toString()).toBe(
      'search=ventas&sort=-pendingCritical&page=2&pageSize=50',
    );
    request.flush(pageOf([]));
  });

  it('gets an instance and an account by id', () => {
    api.getInstance('i1').subscribe();
    api.getAccount('a1').subscribe();

    http.expectOne({ method: 'GET', url: '/api/v1/sql-instances/i1' }).flush({});
    http.expectOne({ method: 'GET', url: '/api/v1/sql-accounts/a1' }).flush({});
  });

  it('searches the accounts of an instance, pending by default in the page (P-15)', () => {
    api
      .searchAccounts('i1', {
        level: 'database',
        database: 'Ventas',
        type: 'database-user',
        risk: 'critical',
        validation: 'pending',
        search: 'app',
        sort: { active: 'name', descending: false },
        page: 1,
        pageSize: 20,
      })
      .subscribe();

    const request = http.expectOne((r) => r.url === '/api/v1/sql-instances/i1/accounts');
    expect(request.request.params.toString()).toBe(
      'level=database&database=Ventas&type=database-user&risk=critical&validation=pending&search=app&sort=name&page=1&pageSize=20',
    );
    request.flush(pageOf([]));
  });

  it('searches the permissions of an instance', () => {
    api
      .searchPermissions('i1', {
        risk: 'denied',
        validation: '',
        database: '',
        account: 'app',
        objectType: 'table',
        search: 'Facturas',
        sort: { active: 'risk', descending: true },
        page: 3,
        pageSize: 100,
      })
      .subscribe();

    const request = http.expectOne((r) => r.url === '/api/v1/sql-instances/i1/permissions');
    expect(request.request.params.toString()).toBe(
      'risk=denied&account=app&objectType=table&search=Facturas&sort=-risk&page=3&pageSize=100',
    );
    request.flush(pageOf([]));
  });

  it('gets the elements and the change log of an account', () => {
    api
      .getAccountItems('a1', {
        kind: 'permission',
        risk: '',
        validation: 'valid',
        database: 'Ventas',
        objectType: '',
        page: 1,
        pageSize: 20,
      })
      .subscribe();
    api.getAccountChanges('a1', 2, 10).subscribe();

    const items = http.expectOne((r) => r.url === '/api/v1/sql-accounts/a1/items');
    expect(items.request.params.toString()).toBe(
      'kind=permission&validation=valid&database=Ventas&page=1&pageSize=20',
    );
    items.flush(pageOf([]));
    const changes = http.expectOne((r) => r.url === '/api/v1/sql-accounts/a1/changes');
    expect(changes.request.params.toString()).toBe('page=2&pageSize=10');
    changes.flush(pageOf([]));
  });

  it('RN-6: validates an account with the fingerprint the user saw', async () => {
    const result = firstValueFrom(api.validateAccount('a1', 'ab'.repeat(32)));

    const request = http.expectOne('/api/v1/sql-accounts/a1/validation');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ fingerprint: 'ab'.repeat(32) });
    request.flush(valid);

    expect(await result).toEqual(valid);
  });

  it('unvalidates an account with DELETE and reports it pending again', async () => {
    const result = firstValueFrom(api.unvalidateAccount('a1'));

    const request = http.expectOne('/api/v1/sql-accounts/a1/validation');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(await result).toEqual({ status: 'pending', by: null, at: null });
  });

  it('validates and unvalidates role memberships and permissions without a body', async () => {
    const marked = firstValueFrom(api.validateItem('p1'));
    const put = http.expectOne('/api/v1/sql-access-items/p1/validation');
    expect(put.request.method).toBe('PUT');
    expect(put.request.body).toBeNull();
    put.flush(valid);
    expect(await marked).toEqual(valid);

    const unmarked = firstValueFrom(api.unvalidateItem('p1'));
    const del = http.expectOne('/api/v1/sql-access-items/p1/validation');
    expect(del.request.method).toBe('DELETE');
    del.flush(null, { status: 204, statusText: 'No Content' });
    expect((await unmarked).status).toBe('pending');
  });
});
