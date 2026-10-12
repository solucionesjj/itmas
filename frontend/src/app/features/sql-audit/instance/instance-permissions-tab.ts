import { Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Sort } from '@angular/material/sort';
import {
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
import {
  AccessColumn,
  AccessTable,
  AccessToggle,
  PERMISSION_COLUMNS,
} from '../components/access-table';
import { SqlStatus } from '../components/sql-status';
import { SqlAuditApi } from '../data/sql-audit-api';
import { options } from '../data/sql-audit-labels';
import {
  ItemValidationFilter,
  Paged,
  PermissionSort,
  RiskLevel,
  Sorting,
  SqlAccessItem,
  SqlInstanceDetail,
} from '../data/sql-audit.models';
import { PendingChange, SqlValidation } from '../data/sql-validation';

const DEFAULT_SORT: Sorting<PermissionSort> = { active: 'risk', descending: true };

/**
 * All the permissions of an instance in one table (SPEC-0005 RF-23, P-25), e.g. "critical and pending". Server-side
 * paging, sorting and filtering: an instance can have tens of thousands of permissions (RNF-5).
 */
@Component({
  selector: 'app-instance-permissions-tab',
  imports: [
    AccessTable,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    SqlStatus,
  ],
  templateUrl: './instance-permissions-tab.html',
  styleUrl: '../sql-audit-page.scss',
})
export class InstancePermissionsTab {
  private readonly api = inject(SqlAuditApi);
  private readonly validation = inject(SqlValidation);
  private readonly auth = inject(AuthService);

  readonly instance = input.required<SqlInstanceDetail>();
  readonly pendingChange = output<PendingChange>();

  protected readonly columns: readonly AccessColumn[] = ['account', ...PERMISSION_COLUMNS];
  protected readonly search = signal('');
  protected readonly account = signal('');
  protected readonly risk = signal<RiskLevel | ''>('');
  protected readonly validationFilter = signal<ItemValidationFilter>('pending');
  protected readonly database = signal('');
  protected readonly objectType = signal('');
  protected readonly sort = signal<Sorting<PermissionSort>>(DEFAULT_SORT);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly result = signal<Paged<SqlAccessItem> | null>(null);
  private readonly reload = new Subject<void>();

  private readonly instanceId = computed(() => this.instance().id);
  protected readonly databases = computed(() => this.instance().databases.map((db) => db.name));
  protected readonly objectTypes = computed(() =>
    options('objectType', this.instance().objectTypes),
  );
  protected readonly canValidate = computed(() =>
    this.auth.hasPermission('sql-audit.accesses.validate'),
  );
  protected readonly emptyText = computed(() => {
    if (this.loading()) {
      return 'Cargando…';
    }
    const onlyPending =
      this.validationFilter() === 'pending' &&
      this.search() === '' &&
      this.account() === '' &&
      this.risk() === '' &&
      this.database() === '' &&
      this.objectType() === '';
    return onlyPending
      ? 'No hay elementos pendientes de validar.'
      : 'Ningún permiso coincide con los filtros.';
  });

  constructor() {
    combineLatest([
      toObservable(this.instanceId),
      toObservable(this.search).pipe(debounceTime(300), distinctUntilChanged()),
      toObservable(this.account).pipe(debounceTime(300), distinctUntilChanged()),
      toObservable(this.risk),
      toObservable(this.validationFilter),
      toObservable(this.database),
      toObservable(this.objectType),
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
          ([
            id,
            search,
            account,
            risk,
            validation,
            database,
            objectType,
            sort,
            pageIndex,
            pageSize,
          ]) =>
            this.api
              .searchPermissions(id, {
                search,
                account,
                risk,
                validation,
                database,
                objectType,
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

  protected onSort(event: Sort): void {
    this.pageIndex.set(0);
    this.sort.set(
      event.direction
        ? { active: event.active as PermissionSort, descending: event.direction === 'desc' }
        : DEFAULT_SORT,
    );
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  protected retry(): void {
    this.reload.next();
  }

  protected toggle({ item, valid }: AccessToggle): void {
    this.validation
      .toggleItem(this.result, item, valid, (delta) =>
        this.pendingChange.emit({ delta, critical: item.risk === 'critical' }),
      )
      .pipe(filter((outcome) => outcome === 'outdated'))
      .subscribe(() => this.reload.next());
  }
}
