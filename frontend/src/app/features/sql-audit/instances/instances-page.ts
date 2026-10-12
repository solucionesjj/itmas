import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
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
  Subject,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  of,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { SqlStatus } from '../components/sql-status';
import { SqlAuditApi } from '../data/sql-audit-api';
import {
  DELETED_DEVICE_STATUS,
  UNAVAILABLE_STATUS,
  destination,
  engineLabel,
  tlsStatus,
} from '../data/sql-audit-labels';
import {
  InstanceSort,
  Paged,
  Sorting,
  SqlEngine,
  SqlInstanceListItem,
} from '../data/sql-audit.models';

/** Manual process explaining how to configure the agent connections (SPEC-0005 §17). */
export const CONFIGURE_CONNECTIONS_MANUAL = ['/manual', 'configurar-conexiones-sql'];

/**
 * Audited SQL instances (SPEC-0005 RF-20): device, alias, engine, target, last collection, accounts and pending
 * elements, with warnings for unreadable parts and unverified TLS.
 */
@Component({
  selector: 'app-sql-instances-page',
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
    RouterLink,
    SqlStatus,
  ],
  templateUrl: './instances-page.html',
  styleUrl: '../sql-audit-page.scss',
})
export class InstancesPage {
  private readonly api = inject(SqlAuditApi);

  protected readonly columns = [
    'alias',
    'engine',
    'destination',
    'device',
    'lastCollectedAt',
    'accountCount',
    'pending',
    'pendingCritical',
  ];
  protected readonly search = signal('');
  protected readonly engine = signal<SqlEngine | ''>('');
  protected readonly sort = signal<Sorting<InstanceSort>>({ active: 'alias', descending: false });
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly result = signal<Paged<SqlInstanceListItem> | null>(null);
  private readonly reload = new Subject<void>();

  protected readonly manualLink = CONFIGURE_CONNECTIONS_MANUAL;
  protected readonly engineLabel = engineLabel;
  protected readonly destination = destination;
  protected readonly tlsStatus = tlsStatus;
  protected readonly unavailableStatus = UNAVAILABLE_STATUS;
  protected readonly deletedDeviceStatus = DELETED_DEVICE_STATUS;

  /** Filters at their defaults: an empty result then means nothing is audited yet (§8.4). */
  protected readonly unfiltered = computed(() => this.search() === '' && this.engine() === '');

  constructor() {
    combineLatest([
      toObservable(this.search).pipe(debounceTime(300), distinctUntilChanged()),
      toObservable(this.engine),
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
        switchMap(([search, engine, sort, pageIndex, pageSize]) =>
          this.api.searchInstances({ search, engine, sort, page: pageIndex + 1, pageSize }).pipe(
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

  protected onSearch(value: string): void {
    this.pageIndex.set(0);
    this.search.set(value.trim());
  }

  protected onEngine(value: SqlEngine | ''): void {
    this.pageIndex.set(0);
    this.engine.set(value);
  }

  protected onSort(event: Sort): void {
    const active = (event.direction ? event.active : 'alias') as InstanceSort;
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
}
