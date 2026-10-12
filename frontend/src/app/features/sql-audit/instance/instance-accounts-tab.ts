import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import {
  EMPTY,
  Subject,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  filter,
  of,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { RiskChip } from '../components/risk-chip';
import { SqlStatus } from '../components/sql-status';
import { ValidationCheckbox } from '../components/validation-checkbox';
import { SqlAuditApi } from '../data/sql-audit-api';
import {
  STALE_STATUS,
  accountName,
  levelLabel,
  options,
  sqlLabel,
  validationStatus,
} from '../data/sql-audit-labels';
import {
  AccountLevel,
  AccountRisk,
  AccountSort,
  AccountValidationFilter,
  Paged,
  Sorting,
  SqlAccountListItem,
  SqlInstanceDetail,
} from '../data/sql-audit.models';
import {
  PendingChange,
  SqlValidation,
  currentStatus,
  patchRow,
  pendingDelta,
} from '../data/sql-validation';

/**
 * Accounts of an instance (SPEC-0005 RF-21): one row per account with its level, type, risk, roles, pending elements
 * and "Válido" checkbox. Shows the accounts with pending elements by default (P-15).
 */
@Component({
  selector: 'app-instance-accounts-tab',
  imports: [
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    RiskChip,
    RouterLink,
    SqlStatus,
    ValidationCheckbox,
  ],
  templateUrl: './instance-accounts-tab.html',
  styleUrl: '../sql-audit-page.scss',
})
export class InstanceAccountsTab {
  private readonly api = inject(SqlAuditApi);
  protected readonly validation = inject(SqlValidation);
  private readonly auth = inject(AuthService);

  readonly instance = input.required<SqlInstanceDetail>();
  readonly pendingChange = output<PendingChange>();

  protected readonly columns = [
    'level',
    'name',
    'type',
    'enabled',
    'risk',
    'roles',
    'pending',
    'status',
    'firstSeenAt',
    'valid',
  ];
  protected readonly search = signal('');
  protected readonly level = signal<AccountLevel | ''>('');
  protected readonly database = signal('');
  protected readonly type = signal('');
  protected readonly risk = signal<AccountRisk | ''>('');
  protected readonly validationFilter = signal<AccountValidationFilter>('pending');
  protected readonly sort = signal<Sorting<AccountSort>>({ active: 'name', descending: false });
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly result = signal<Paged<SqlAccountListItem> | null>(null);
  private readonly reload = new Subject<void>();

  private readonly instanceId = computed(() => this.instance().id);
  protected readonly databases = computed(() => this.instance().databases.map((db) => db.name));
  protected readonly accountTypes = computed(() =>
    options('accountType', this.instance().accountTypes),
  );
  protected readonly canValidate = computed(() =>
    this.auth.hasPermission('sql-audit.accesses.validate'),
  );
  /** Only the default "pending" filter is active: an empty result means nothing is left to validate (§8.4). */
  protected readonly onlyPending = computed(
    () =>
      this.validationFilter() === 'pending' &&
      this.search() === '' &&
      this.level() === '' &&
      this.database() === '' &&
      this.type() === '' &&
      this.risk() === '',
  );

  protected readonly label = sqlLabel;
  protected readonly levelLabel = levelLabel;
  protected readonly accountName = accountName;
  protected readonly currentStatus = currentStatus;
  protected readonly validationStatus = validationStatus;
  protected readonly staleStatus = STALE_STATUS;

  constructor() {
    combineLatest([
      toObservable(this.instanceId),
      toObservable(this.search).pipe(debounceTime(300), distinctUntilChanged()),
      toObservable(this.level),
      toObservable(this.database),
      toObservable(this.type),
      toObservable(this.risk),
      toObservable(this.validationFilter),
      toObservable(this.sort),
      toObservable(this.pageIndex),
      toObservable(this.pageSize),
      this.reload.pipe(startWith(undefined)),
    ])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
        }),
        switchMap(
          ([id, search, level, database, type, risk, validation, sort, pageIndex, pageSize]) =>
            this.api
              .searchAccounts(id, {
                search,
                level,
                database,
                type,
                risk,
                validation,
                sort,
                page: pageIndex + 1,
                pageSize,
              })
              .pipe(
                catchError(() => {
                  this.failed.set(true);
                  return of(null);
                }),
              ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
      });
  }

  /** Sets a filter and goes back to the first page. */
  protected setFilter<T>(target: { set(value: T): void }, value: T): void {
    this.pageIndex.set(0);
    target.set(value);
  }

  protected onLevel(value: AccountLevel | ''): void {
    this.pageIndex.set(0);
    this.level.set(value);
    if (value === 'server') {
      this.database.set('');
    }
  }

  protected onSort(event: Sort): void {
    const active = (event.direction ? event.active : 'name') as AccountSort;
    this.pageIndex.set(0);
    this.sort.set({ active, descending: event.direction === 'desc' });
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  protected retry(): void {
    this.reload.next();
  }

  /** "Válido" of the account (RF-16): optimistic, with the fingerprint the user saw (RN-6). */
  protected toggle(account: SqlAccountListItem, valid: boolean): void {
    this.validation
      .toggle(
        { kind: 'account', id: account.id, fingerprint: account.fingerprint },
        valid,
        account.validation,
        (validation) => {
          const shown = this.result()?.items.find((row) => row.id === account.id);
          const delta = shown ? pendingDelta(shown.validation.status, validation.status) : 0;
          this.result.update((page) =>
            patchRow(page, account.id, (row) => ({
              ...row,
              validation,
              pendingCount: Math.max(0, row.pendingCount + delta),
            })),
          );
          if (delta !== 0) {
            this.pendingChange.emit({ delta, critical: false });
          }
        },
      )
      .pipe(
        filter((outcome) => outcome === 'outdated'),
        switchMap(() => this.api.getAccount(account.id).pipe(catchError(() => EMPTY))),
      )
      .subscribe((fresh) => this.result.update((page) => patchRow(page, fresh.id, () => fresh)));
  }
}
