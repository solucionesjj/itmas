import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Subject, catchError, combineLatest, of, startWith, switchMap, tap } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { RiskChip } from '../components/risk-chip';
import { SqlStatus } from '../components/sql-status';
import { ValidationCheckbox } from '../components/validation-checkbox';
import {
  DELETED_DEVICE_STATUS,
  StatusPresentation,
  accountName,
  destination,
  levelLabel,
  sqlLabel,
  tlsStatus,
  validationStatus,
  yesNo,
} from '../data/sql-audit-labels';
import { SqlAuditApi } from '../data/sql-audit-api';
import { SqlAccountDetail } from '../data/sql-audit.models';
import {
  SqlValidation,
  ValidationOutcome,
  currentStatus,
  pendingDelta,
} from '../data/sql-validation';
import { AccountHistory } from './account-history';
import { AccountItems } from './account-items';

/**
 * Detail of an account (SPEC-0005 RF-22): its attributes, risk (own and inherited, P-24) and "Válido" checkbox; its
 * roles and permissions, each with its own checkbox; and the change log with the validation history.
 */
@Component({
  selector: 'app-sql-account-page',
  imports: [
    AccountHistory,
    AccountItems,
    DatePipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    RiskChip,
    RouterLink,
    SqlStatus,
    ValidationCheckbox,
  ],
  templateUrl: './account-page.html',
  styleUrl: '../sql-audit-page.scss',
})
export class AccountPage {
  private readonly api = inject(SqlAuditApi);
  protected readonly validation = inject(SqlValidation);
  private readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly account = signal<SqlAccountDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly failed = signal(false);
  /** Increases after every saved validation so the change log shows it. */
  protected readonly historyVersion = signal(0);
  private readonly reload = new Subject<void>();

  protected readonly canValidate = computed(() =>
    this.auth.hasPermission('sql-audit.accesses.validate'),
  );
  protected readonly status = computed(() => {
    const account = this.account();
    return account ? currentStatus(account) : 'pending';
  });
  protected readonly name = computed(() => {
    const account = this.account();
    return account ? accountName(account) : '';
  });

  /** Warnings of the account: removed, changed since its validation, not refreshed, unverified TLS, deleted device. */
  protected readonly notices = computed<StatusPresentation[]>(() => {
    const account = this.account();
    if (!account) {
      return [];
    }
    const notices: StatusPresentation[] = [];
    if (account.removedAt) {
      notices.push({
        ...validationStatus('removed'),
        label: 'Esta cuenta ya no existe en el motor. Se conserva para consultar su historial.',
      });
    } else if (account.validation.status === 'changed') {
      notices.push({
        ...validationStatus('changed'),
        label:
          'Esta cuenta cambió desde su validación. Revisa el registro de cambios y vuelve a validarla.',
      });
    }
    if (account.stale) {
      notices.push({
        label:
          'No se pudo leer en la última recolección: los datos de esta cuenta pueden no estar actualizados.',
        icon: 'warning',
        tone: 'tertiary',
      });
    }
    const tls = tlsStatus(account.instance.tls);
    if (tls) {
      notices.push({
        ...tls,
        label:
          'Certificado sin verificar: la conexión va cifrada, pero no se comprobó la identidad del servidor.',
      });
    }
    if (account.instance.device.status === 'deleted') {
      notices.push({
        ...DELETED_DEVICE_STATUS,
        label: 'El equipo que auditaba esta instancia fue eliminado: no envía datos nuevos.',
      });
    }
    return notices;
  });

  protected readonly label = sqlLabel;
  protected readonly yesNo = yesNo;
  protected readonly levelLabel = levelLabel;
  protected readonly destination = destination;
  protected readonly validationStatus = validationStatus;

  constructor() {
    combineLatest([toObservable(this.id), this.reload.pipe(startWith(undefined))])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
          this.notFound.set(false);
        }),
        switchMap(([id]) =>
          this.api.getAccount(id).pipe(
            catchError((error: unknown) => {
              if (error instanceof ApiError && error.status === 404) {
                this.notFound.set(true);
              } else {
                this.failed.set(true);
              }
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((account) => {
        this.account.set(account);
        this.loading.set(false);
      });
  }

  /** "Válido" of the account (RF-16) with the fingerprint shown (RN-6). */
  protected toggle(valid: boolean): void {
    const account = this.account();
    if (!account) {
      return;
    }
    this.validation
      .toggle(
        { kind: 'account', id: account.id, fingerprint: account.fingerprint },
        valid,
        account.validation,
        (validation) =>
          this.account.update((current) =>
            current
              ? {
                  ...current,
                  validation,
                  pendingCount: Math.max(
                    0,
                    current.pendingCount +
                      pendingDelta(current.validation.status, validation.status),
                  ),
                }
              : current,
          ),
      )
      .subscribe((outcome) => this.onSettled(outcome));
  }

  protected onPendingDelta(delta: number): void {
    this.account.update((current) =>
      current ? { ...current, pendingCount: Math.max(0, current.pendingCount + delta) } : current,
    );
  }

  /** After a validation: show it in the log; if the element changed meanwhile, reload the account (409/422). */
  protected onSettled(outcome: ValidationOutcome): void {
    if (outcome === 'outdated') {
      this.reload.next();
    }
    if (outcome !== 'failed') {
      this.historyVersion.update((version) => version + 1);
    }
  }

  protected retry(): void {
    this.reload.next();
  }
}
