import { Injectable, WritableSignal, inject, signal } from '@angular/core';
import { Observable, catchError, finalize, map, of } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { problemMessage } from '../../../core/models/problem-message';
import { NotificationService } from '../../../core/services/notification-service';
import { SqlAuditApi } from './sql-audit-api';
import { Paged, SqlAccessItem, Validation, ValidationStatus } from './sql-audit.models';

export const SAVE_ERROR_MESSAGE = 'No se pudo guardar la validación. Inténtalo de nuevo.';
export const ACCOUNT_CONFLICT_MESSAGE =
  'Esta cuenta cambió desde que cargaste la página. Revisa los cambios y vuelve a validarla.';

/** Element whose "Válido" checkbox was clicked. Accounts carry the fingerprint the user saw (RN-6). */
export type ValidationTarget =
  | { readonly kind: 'account'; readonly id: string; readonly fingerprint: string }
  | { readonly kind: 'item'; readonly id: string };

/**
 * - `saved`: the API confirmed the change.
 * - `failed`: reverted; the user was told to try again.
 * - `outdated`: reverted because the element changed (`409`) or was removed (`422`): the caller reloads it.
 */
export type ValidationOutcome = 'saved' | 'failed' | 'outdated';

/** Change of the pending counters made by a "Válido" checkbox, reported to the page header. */
export interface PendingChange {
  readonly delta: number;
  /** The element is critical: the critical counter changes too. */
  readonly critical: boolean;
}

/** Whether an element in this state counts as pending (RF-18): current and not valid. */
export function isPending(status: ValidationStatus): boolean {
  return status === 'pending' || status === 'changed';
}

/** Change of the pending counter when an element goes from one state to another. */
export function pendingDelta(before: ValidationStatus, after: ValidationStatus): number {
  return Number(isPending(after)) - Number(isPending(before));
}

/** Replaces one row of a page, by id. */
export function patchRow<T extends { readonly id: string }>(
  page: Paged<T> | null,
  id: string,
  patch: (row: T) => T,
): Paged<T> | null {
  return page
    ? { ...page, items: page.items.map((row) => (row.id === id ? patch(row) : row)) }
    : page;
}

/**
 * "Válido" checkboxes (RF-16): one click marks, another unmarks, without dialogs. The change is shown at once
 * (optimistic update) and reverted with a notice if the API rejects it.
 */
@Injectable({ providedIn: 'root' })
export class SqlValidation {
  private readonly api = inject(SqlAuditApi);
  private readonly notifications = inject(NotificationService);
  private readonly inFlight = signal<ReadonlySet<string>>(new Set());

  /** Elements with a request in progress: their checkbox ignores clicks until it settles. */
  isSaving(id: string): boolean {
    return this.inFlight().has(id);
  }

  /**
   * Marks (`valid = true`) or unmarks the element. `apply` receives the validation to show: first the expected one,
   * then the API's, or the previous one if the request fails. Callers derive their pending counters from it.
   */
  toggle(
    target: ValidationTarget,
    valid: boolean,
    previous: Validation,
    apply: (validation: Validation) => void,
  ): Observable<ValidationOutcome> {
    apply(
      valid
        ? { status: 'valid', by: null, at: new Date().toISOString() }
        : { status: 'pending', by: null, at: null },
    );
    this.track(target.id, true);
    return this.request(target, valid).pipe(
      map((validation): ValidationOutcome => {
        apply(validation);
        return 'saved';
      }),
      catchError((error: unknown) => {
        apply(previous);
        return of(this.report(target, error));
      }),
      finalize(() => this.track(target.id, false)),
    );
  }

  /**
   * Marks or unmarks a role membership or a permission shown in `rows`, and reports each change of the pending
   * counter so the page can update its totals at once (RF-18).
   */
  toggleItem(
    rows: WritableSignal<Paged<SqlAccessItem> | null>,
    item: SqlAccessItem,
    valid: boolean,
    onPendingDelta: (delta: number) => void,
  ): Observable<ValidationOutcome> {
    return this.toggle({ kind: 'item', id: item.id }, valid, item.validation, (validation) => {
      const shown = rows()?.items.find((row) => row.id === item.id);
      const delta = shown ? pendingDelta(shown.validation.status, validation.status) : 0;
      rows.update((page) => patchRow(page, item.id, (row) => ({ ...row, validation })));
      if (delta !== 0) {
        onPendingDelta(delta);
      }
    });
  }

  private request(target: ValidationTarget, valid: boolean): Observable<Validation> {
    if (target.kind === 'account') {
      return valid
        ? this.api.validateAccount(target.id, target.fingerprint)
        : this.api.unvalidateAccount(target.id);
    }
    return valid ? this.api.validateItem(target.id) : this.api.unvalidateItem(target.id);
  }

  private report(target: ValidationTarget, error: unknown): ValidationOutcome {
    const status = error instanceof ApiError ? error.status : 0;
    if (status === 409 && target.kind === 'account') {
      this.notifications.error(ACCOUNT_CONFLICT_MESSAGE);
      return 'outdated';
    }
    if (status === 422) {
      this.notifications.error(problemMessage(error, SAVE_ERROR_MESSAGE));
      return 'outdated';
    }
    const traceId = error instanceof ApiError ? error.problem?.traceId : undefined;
    this.notifications.error(
      status >= 500 && traceId
        ? `${SAVE_ERROR_MESSAGE.slice(0, -1)} (ref. ${traceId}).`
        : SAVE_ERROR_MESSAGE,
    );
    return 'failed';
  }

  private track(id: string, saving: boolean): void {
    this.inFlight.update((current) => {
      const next = new Set(current);
      if (saving) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }
}

/** State to show for an element: removed from the engine wins over any validation (RF-17). */
export function currentStatus(element: {
  readonly validation: Validation;
  readonly removedAt: string | null;
}): ValidationStatus {
  return element.removedAt ? 'removed' : element.validation.status;
}
