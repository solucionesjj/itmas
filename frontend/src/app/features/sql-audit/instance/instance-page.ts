import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  Subject,
  catchError,
  combineLatest,
  debounceTime,
  of,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { SqlStatus } from '../components/sql-status';
import { SqlAuditApi } from '../data/sql-audit-api';
import {
  DELETED_DEVICE_STATUS,
  StatusPresentation,
  destination,
  engineLabel,
  sqlLabel,
  tlsStatus,
  unavailableMessage,
} from '../data/sql-audit-labels';
import { SqlInstanceDetail } from '../data/sql-audit.models';
import { PendingChange } from '../data/sql-validation';
import { InstanceAccountsTab } from './instance-accounts-tab';
import { InstancePermissionsTab } from './instance-permissions-tab';

const TABS = ['accounts', 'permissions'] as const;

/**
 * One audited instance (SPEC-0005 §11): its data and warnings, and the tabs "Cuentas" (RF-21) and "Permisos" (RF-23).
 * The selected tab is kept in the URL (`?tab=permissions`) so that "back" returns to it.
 */
@Component({
  selector: 'app-sql-instance-page',
  imports: [
    DatePipe,
    InstanceAccountsTab,
    InstancePermissionsTab,
    MatButtonModule,
    MatCardModule,
    MatExpansionModule,
    MatIconModule,
    MatProgressBarModule,
    MatTabsModule,
    RouterLink,
    SqlStatus,
  ],
  templateUrl: './instance-page.html',
  styleUrl: '../sql-audit-page.scss',
})
export class InstancePage {
  private readonly api = inject(SqlAuditApi);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly id = input.required<string>();
  /** Query parameter: `permissions` selects the second tab. */
  readonly tab = input<string | undefined>(undefined);

  protected readonly instance = signal<SqlInstanceDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly failed = signal(false);
  private readonly reload = new Subject<void>();
  /** Silent reload of the counters after validations, to reconcile the optimistic ones. */
  private readonly reconcile = new Subject<void>();

  protected readonly selectedTab = computed(() =>
    Math.max(0, TABS.indexOf((this.tab() ?? 'accounts') as (typeof TABS)[number])),
  );
  protected readonly engineLabel = engineLabel;
  protected readonly destination = destination;
  protected readonly label = sqlLabel;
  protected readonly deletedDeviceStatus = DELETED_DEVICE_STATUS;

  /** Warnings of the instance: unverified TLS (RF-3, CA-4) and parts not read in the last collection (RF-13). */
  protected readonly notices = computed<StatusPresentation[]>(() => {
    const instance = this.instance();
    if (!instance) {
      return [];
    }
    const tls = tlsStatus(instance.tls);
    return [
      ...(tls
        ? [
            {
              ...tls,
              label:
                'Certificado sin verificar: la conexión va cifrada, pero no se comprobó la identidad del servidor.',
            },
          ]
        : []),
      ...instance.unavailable.map((item) => ({
        label: unavailableMessage(item),
        icon: 'warning',
        tone: 'tertiary' as const,
      })),
    ];
  });

  constructor() {
    combineLatest([toObservable(this.id), this.reload.pipe(startWith(undefined))])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
          this.notFound.set(false);
        }),
        switchMap(([id]) =>
          this.api.getInstance(id).pipe(
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
      .subscribe((instance) => {
        this.instance.set(instance);
        this.loading.set(false);
      });

    this.reconcile
      .pipe(
        debounceTime(1000),
        switchMap(() => this.api.getInstance(this.id()).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(),
      )
      .subscribe((instance) => {
        if (instance) {
          this.instance.set(instance);
        }
      });
  }

  /** Updates the header counters at once (RF-16) and reconciles them with the API shortly after. */
  protected onPendingChange(change: PendingChange): void {
    this.instance.update((instance) =>
      instance
        ? {
            ...instance,
            pendingCount: Math.max(0, instance.pendingCount + change.delta),
            pendingCriticalCount: change.critical
              ? Math.max(0, instance.pendingCriticalCount + change.delta)
              : instance.pendingCriticalCount,
          }
        : instance,
    );
    this.reconcile.next();
  }

  protected onTabChange(index: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab: index === 0 ? null : TABS[index] },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected retry(): void {
    this.reload.next();
  }
}
