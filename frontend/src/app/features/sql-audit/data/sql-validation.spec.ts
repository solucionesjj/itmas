import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Subject } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { NotificationService } from '../../../core/services/notification-service';
import { SqlAuditApi } from './sql-audit-api';
import { pageOf, permissionOf } from './sql-audit-fixtures';
import { Paged, SqlAccessItem, Validation } from './sql-audit.models';
import {
  ACCOUNT_CONFLICT_MESSAGE,
  SAVE_ERROR_MESSAGE,
  SqlValidation,
  ValidationOutcome,
  pendingDelta,
} from './sql-validation';

describe('SqlValidation', () => {
  let response: Subject<Validation>;
  const api = {
    validateAccount: vi.fn(),
    unvalidateAccount: vi.fn(),
    validateItem: vi.fn(),
    unvalidateItem: vi.fn(),
  };
  const notifications = { error: vi.fn(), info: vi.fn() };
  let service: SqlValidation;

  beforeEach(() => {
    response = new Subject<Validation>();
    for (const method of Object.values(api)) {
      method.mockReset().mockReturnValue(response);
    }
    notifications.error.mockReset();
    TestBed.configureTestingModule({
      providers: [
        { provide: SqlAuditApi, useValue: api },
        { provide: NotificationService, useValue: notifications },
      ],
    });
    service = TestBed.inject(SqlValidation);
  });

  const pending: Validation = { status: 'pending', by: null, at: null };
  const valid: Validation = { status: 'valid', by: 'Ana', at: '2026-10-11T10:00:00Z' };

  it('counts pending elements: pending and changed are, valid and removed are not (RF-18)', () => {
    expect(pendingDelta('pending', 'valid')).toBe(-1);
    expect(pendingDelta('changed', 'valid')).toBe(-1);
    expect(pendingDelta('valid', 'pending')).toBe(1);
    expect(pendingDelta('valid', 'valid')).toBe(0);
    expect(pendingDelta('removed', 'removed')).toBe(0);
  });

  it('RF-16: shows the change at once, then the API result', () => {
    const shown: Validation[] = [];
    const outcomes: ValidationOutcome[] = [];

    service
      .toggle({ kind: 'item', id: 'p1' }, true, pending, (v) => shown.push(v))
      .subscribe((outcome) => outcomes.push(outcome));

    expect(api.validateItem).toHaveBeenCalledWith('p1');
    expect(shown.map((v) => v.status)).toEqual(['valid']);
    expect(service.isSaving('p1')).toBe(true);

    response.next(valid);
    response.complete();

    expect(shown.at(-1)).toEqual(valid);
    expect(outcomes).toEqual(['saved']);
    expect(service.isSaving('p1')).toBe(false);
    expect(notifications.error).not.toHaveBeenCalled();
  });

  it('RN-6: validates accounts with their fingerprint and unmarks them with DELETE', () => {
    service
      .toggle({ kind: 'account', id: 'a1', fingerprint: 'abc' }, true, pending, () => undefined)
      .subscribe();
    service
      .toggle({ kind: 'account', id: 'a2', fingerprint: 'def' }, false, valid, () => undefined)
      .subscribe();

    expect(api.validateAccount).toHaveBeenCalledWith('a1', 'abc');
    expect(api.unvalidateAccount).toHaveBeenCalledWith('a2');
  });

  it('§8.4: reverts and asks to try again when saving fails', () => {
    const shown: Validation[] = [];
    const outcomes: ValidationOutcome[] = [];

    service
      .toggle({ kind: 'item', id: 'p1' }, false, valid, (v) => shown.push(v))
      .subscribe((outcome) => outcomes.push(outcome));
    response.error(new ApiError(0, null));

    expect(api.unvalidateItem).toHaveBeenCalledWith('p1');
    expect(shown.map((v) => v.status)).toEqual(['pending', 'valid']);
    expect(shown.at(-1)).toEqual(valid);
    expect(outcomes).toEqual(['failed']);
    expect(notifications.error).toHaveBeenCalledWith(SAVE_ERROR_MESSAGE);
  });

  it('CA-12: a 409 on an account reverts, explains the change and asks to reload it', () => {
    const outcomes: ValidationOutcome[] = [];

    service
      .toggle({ kind: 'account', id: 'a1', fingerprint: 'old' }, true, pending, () => undefined)
      .subscribe((outcome) => outcomes.push(outcome));
    response.error(new ApiError(409, { title: 'Conflict', status: 409 }));

    expect(outcomes).toEqual(['outdated']);
    expect(notifications.error).toHaveBeenCalledWith(ACCOUNT_CONFLICT_MESSAGE);
  });

  it('CA-12: a 422 on a removed element shows the API detail and asks to reload it', () => {
    const outcomes: ValidationOutcome[] = [];

    service
      .toggle({ kind: 'item', id: 'p1' }, true, pending, () => undefined)
      .subscribe((outcome) => outcomes.push(outcome));
    response.error(
      new ApiError(422, {
        title: 'Unprocessable',
        status: 422,
        detail: 'El permiso ya no existe.',
      }),
    );

    expect(outcomes).toEqual(['outdated']);
    expect(notifications.error).toHaveBeenCalledWith('El permiso ya no existe.');
  });

  it('updates the row and reports each change of the pending counter', () => {
    const rows = signal<Paged<SqlAccessItem> | null>(
      pageOf([permissionOf(), permissionOf({ id: 'p2' })]),
    );
    const deltas: number[] = [];

    service.toggleItem(rows, permissionOf(), true, (delta) => deltas.push(delta)).subscribe();
    expect(rows()!.items.map((row) => row.validation.status)).toEqual(['valid', 'pending']);
    expect(deltas).toEqual([-1]);

    response.error(new ApiError(500, { title: 'Error', status: 500, traceId: 'abc' }));

    expect(rows()!.items.map((row) => row.validation.status)).toEqual(['pending', 'pending']);
    expect(deltas).toEqual([-1, 1]);
    expect(notifications.error).toHaveBeenCalledWith(
      'No se pudo guardar la validación. Inténtalo de nuevo (ref. abc).',
    );
  });
});
