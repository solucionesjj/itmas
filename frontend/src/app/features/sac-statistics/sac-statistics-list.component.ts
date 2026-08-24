import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/t.pipe';
import { ViewError, toViewError } from '../../core/utils/api-error.util';
import {
  AppliedFilter,
  FilterMetaMap,
  anyFilterActive,
  describeFilters,
  filtersFromParams,
  syncFiltersToUrl
} from '../../core/utils/filter-url.util';
import { MessageKey } from '../../core/i18n/messages.es-CO';
import { formatDecimal } from './decimal-format.util';
import { SacStatisticsService } from './sac-statistics.service';
import {
  SacExportFormat,
  SacStatistic,
  SacStatisticSortField,
  SortOrder
} from './sac-statistic.model';

const DEFAULT_SORT: SacStatisticSortField = 'generatedAt';

/** Chip metadata as message keys (§10.1) — never a raw field name. */
const FILTER_META: FilterMetaMap = {
  databaseName: { label: 'field.databaseName' },
  from: { label: 'sac.generatedFrom' },
  to: { label: 'sac.generatedTo' }
};

@Component({
  selector: 'app-sac-statistics-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSortModule,
    MatTableModule,
    TranslatePipe
  ],
  templateUrl: './sac-statistics-list.component.html',
  styleUrl: './sac-statistics-list.component.scss'
})
export class SacStatisticsListComponent {
  private readonly sacStatisticsService = inject(SacStatisticsService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly i18n = inject(I18nService);

  protected readonly displayedColumns = [
    'databaseName',
    'generatedAt',
    'totalSizeGb',
    'dataSizeGb',
    'logSizeGb',
    'debtors',
    'debtorActiveAccounts',
    'activeAccounts',
    'balanceSum',
    'overdueSum',
    'principalSum',
    'activities',
    'activitiesLast30Days',
    'activeUsers',
    'avgActiveUsersLast3Months'
  ];

  protected readonly items = signal<SacStatistic[]>([]);
  protected readonly total = signal(0);
  protected readonly error = signal<ViewError | null>(null);

  // §10.4: skeletons on the first load, a 2px bar over stale rows on a refresh.
  protected readonly loading = signal(false);
  protected readonly firstLoad = signal(true);
  protected readonly showSkeletons = computed(() => this.loading() && this.firstLoad());
  protected readonly refreshing = computed(() => this.loading() && !this.firstLoad());

  /** Drives the two different empty messages §10.4 requires. */
  protected readonly filtersActive = signal(false);
  protected readonly appliedFilters = signal<AppliedFilter[]>([]);
  protected readonly exporting = signal(false);

  protected readonly sortBy = signal<SacStatisticSortField>(DEFAULT_SORT);
  protected readonly sortDir = signal<SortOrder>('desc');

  protected readonly filters = this.fb.nonNullable.group({
    databaseName: '',
    from: '',
    to: ''
  });

  private page = 0;
  protected readonly pageSize = 20;

  constructor() {
    // The URL is the source of truth on entry, so a filtered view is linkable.
    this.filters.patchValue(
      filtersFromParams(this.route.snapshot.queryParams, Object.keys(this.filters.controls)),
      { emitEvent: false }
    );
    this.reload();

    this.filters.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.page = 0;
        // Only a filter change writes the URL — never the constructor. See
        // syncFiltersToUrl's warning about NavigationSkipped and the sidebar.
        syncFiltersToUrl(this.router, this.route, this.filters.getRawValue());
        this.reload();
      });
  }

  protected onPage(event: PageEvent): void {
    this.page = event.pageIndex;
    this.reload();
  }

  protected onSortChange(sort: Sort): void {
    if (!sort.direction) {
      this.sortBy.set(DEFAULT_SORT);
      this.sortDir.set('desc');
    } else {
      this.sortBy.set(sort.active as SacStatisticSortField);
      this.sortDir.set(sort.direction);
    }
    this.page = 0;
    this.reload();
  }

  protected clearFilters(): void {
    this.filters.reset({ databaseName: '', from: '', to: '' });
  }

  protected removeFilter(key: string): void {
    this.filters.get(key)?.setValue('');
  }

  /** Grouped without parsing — see decimal-format.util.ts. */
  protected money(value: string | null): string {
    return formatDecimal(value, this.i18n.locale());
  }

  protected reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const raw = this.filters.getRawValue();
    this.filtersActive.set(anyFilterActive(raw));
    this.appliedFilters.set(
      describeFilters(raw, FILTER_META, (key) => this.i18n.translate(key as MessageKey))
    );

    this.sacStatisticsService
      .list({
        databaseName: raw.databaseName || undefined,
        from: raw.from || undefined,
        to: raw.to || undefined,
        sort: this.sortBy(),
        order: this.sortDir(),
        page: this.page + 1,
        limit: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.total.set(result.total);
          this.loading.set(false);
          this.firstLoad.set(false);
        },
        error: (err) => {
          this.error.set(toViewError(err, this.i18n.translate('sac.error')));
          this.loading.set(false);
          this.firstLoad.set(false);
        }
      });
  }

  protected export(format: SacExportFormat): void {
    if (this.exporting()) {
      return;
    }
    this.exporting.set(true);
    const raw = this.filters.getRawValue();

    this.sacStatisticsService
      .export(format, {
        databaseName: raw.databaseName || undefined,
        from: raw.from || undefined,
        to: raw.to || undefined
      })
      .subscribe({
        next: (response) => {
          this.exporting.set(false);
          this.triggerDownload(response.body, this.filenameFrom(response, format));
        },
        error: (err) => {
          this.exporting.set(false);
          this.snackBar.open(
            toViewError(err, this.i18n.translate('sac.exportFailed')).message,
            this.i18n.translate('action.close'),
            { duration: 4000 }
          );
        }
      });
  }

  private filenameFrom(
    response: { headers: { get(name: string): string | null } },
    format: SacExportFormat
  ): string {
    const disposition = response.headers.get('content-disposition');
    const match = disposition?.match(/filename="?([^";]+)"?/i);
    return match?.[1] ?? `sac-statistics-report.${format}`;
  }

  private triggerDownload(blob: Blob | null, filename: string): void {
    if (!blob) {
      return;
    }
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
