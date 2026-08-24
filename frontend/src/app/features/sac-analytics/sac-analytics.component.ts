import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { forkJoin } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { MessageKey } from '../../core/i18n/messages.es-CO';
import { TranslatePipe } from '../../core/i18n/t.pipe';
import { ViewError, toViewError } from '../../core/utils/api-error.util';
import {
  filtersFromParams,
  syncFiltersToUrl
} from '../../core/utils/filter-url.util';
import { SacAnalyticsService } from './sac-analytics.service';
import {
  GrowthPlot,
  SacGrowthPoint,
  SacMetric,
  SacRankingEntry
} from './sac-analytics.model';
import { SacRankingChartComponent } from './sac-ranking-chart.component';
import { SacGrowthChartComponent } from './sac-growth-chart.component';

/** One growth chart's definition. */
interface GrowthChart {
  metric: SacMetric;
  titleKey: MessageKey;
  captionKey: MessageKey;
  /**
   * `value` for every metric except `activities`. That one is a lifetime
   * counter that never resets, so its raw value is a meaningless ever-growing
   * number and the month's real figure is the DIFFERENCE against the previous
   * month — the activities performed that month. `activitiesLast30Days` is a
   * fixed 30-day window and therefore already comparable month to month, so it
   * plots its own value, and the two deliberately get separate charts rather
   * than sharing an axis (BL-033 CA-6).
   */
  plot: GrowthPlot;
  /**
   * A fixed §2.7 slot per chart, so a metric keeps its colour across reloads.
   *
   * Slot 7 (red) is deliberately unused across all nine: §2.7 notes that slots
   * 5/6/7 double as the compliance triad, and a red column labelled "Saldo"
   * reads as an alert about the balance rather than as an arbitrary series
   * colour. Each chart is a single series in its own card, so the colours are
   * variety rather than a legend — nothing is lost by skipping one.
   */
  colorVar: string;
}

const GROWTH_CHARTS: readonly GrowthChart[] = [
  {
    metric: 'totalSizeGb',
    titleKey: 'sacAnalytics.growthSize',
    captionKey: 'sacAnalytics.growthSizeCaption',
    plot: 'value',
    colorVar: '--chart-1'
  },
  {
    metric: 'activeAccounts',
    titleKey: 'sacAnalytics.growthAccounts',
    captionKey: 'sacAnalytics.growthAccountsCaption',
    plot: 'value',
    colorVar: '--chart-2'
  },
  {
    metric: 'debtors',
    titleKey: 'sacAnalytics.growthDebtors',
    captionKey: 'sacAnalytics.growthDebtorsCaption',
    plot: 'value',
    colorVar: '--chart-3'
  },
  {
    metric: 'activities',
    titleKey: 'sacAnalytics.growthActivities',
    captionKey: 'sacAnalytics.growthActivitiesCaption',
    plot: 'delta',
    colorVar: '--chart-4'
  },
  {
    metric: 'activitiesLast30Days',
    titleKey: 'sacAnalytics.growthActivities30',
    captionKey: 'sacAnalytics.growthActivities30Caption',
    plot: 'value',
    colorVar: '--chart-5'
  },
  {
    metric: 'avgActiveUsersLast3Months',
    titleKey: 'sacAnalytics.growthUsers',
    captionKey: 'sacAnalytics.growthUsersCaption',
    plot: 'value',
    colorVar: '--chart-6'
  },
  {
    metric: 'balanceSum',
    titleKey: 'sacAnalytics.growthBalance',
    captionKey: 'sacAnalytics.growthFinancialCaption',
    plot: 'value',
    colorVar: '--chart-1'
  },
  {
    metric: 'overdueSum',
    titleKey: 'sacAnalytics.growthOverdue',
    captionKey: 'sacAnalytics.growthFinancialCaption',
    plot: 'value',
    colorVar: '--chart-2'
  },
  {
    metric: 'principalSum',
    titleKey: 'sacAnalytics.growthPrincipal',
    captionKey: 'sacAnalytics.growthFinancialCaption',
    plot: 'value',
    colorVar: '--chart-4'
  }
];

/** The ranking's metric selector. Labels are message keys (§12). */
const RANKING_METRICS: readonly { metric: SacMetric; labelKey: MessageKey }[] = [
  { metric: 'totalSizeGb', labelKey: 'field.totalSizeGb' },
  { metric: 'activeAccounts', labelKey: 'field.activeAccounts' },
  { metric: 'debtors', labelKey: 'field.debtors' },
  { metric: 'activities', labelKey: 'field.activities' },
  { metric: 'avgActiveUsersLast3Months', labelKey: 'field.avgActiveUsersLast3Months' },
  { metric: 'balanceSum', labelKey: 'field.balanceSum' },
  { metric: 'overdueSum', labelKey: 'field.overdueSum' },
  { metric: 'principalSum', labelKey: 'field.principalSum' }
];

const MONTH_WINDOWS = [6, 12, 24, 36] as const;

@Component({
  selector: 'app-sac-analytics',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule,
    SacRankingChartComponent,
    SacGrowthChartComponent,
    TranslatePipe
  ],
  templateUrl: './sac-analytics.component.html',
  styleUrl: './sac-analytics.component.scss'
})
export class SacAnalyticsComponent {
  private readonly analyticsService = inject(SacAnalyticsService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  protected readonly i18n = inject(I18nService);

  protected readonly rankingMetrics = RANKING_METRICS;
  protected readonly growthCharts = GROWTH_CHARTS;
  protected readonly monthWindows = MONTH_WINDOWS;

  protected readonly databases = signal<string[]>([]);
  protected readonly ranking = signal<SacRankingEntry[]>([]);
  protected readonly rankingMetric = signal<SacMetric>('totalSizeGb');
  /** metric → that metric's series for the selected database. */
  protected readonly growth = signal<Record<string, SacGrowthPoint[]>>({});
  protected readonly error = signal<ViewError | null>(null);

  // §10.4: skeletons on the first load, a 2px bar over stale figures on a refresh.
  protected readonly loading = signal(false);
  protected readonly firstLoad = signal(true);
  protected readonly showSkeletons = computed(() => this.loading() && this.firstLoad());
  protected readonly refreshing = computed(() => this.loading() && !this.firstLoad());

  protected readonly hasAnyData = computed(
    () => this.databases().length > 0
  );

  protected readonly filters = this.fb.nonNullable.group({
    metric: 'totalSizeGb' as SacMetric,
    databaseName: '',
    months: 12
  });

  constructor() {
    // The URL is the source of truth on entry, so a particular database's
    // analysis is linkable.
    const params = filtersFromParams(this.route.snapshot.queryParams, [
      'metric',
      'databaseName',
      'months'
    ]);
    this.filters.patchValue(
      {
        ...(params['metric'] ? { metric: params['metric'] as SacMetric } : {}),
        ...(params['databaseName'] ? { databaseName: params['databaseName'] } : {}),
        ...(params['months'] ? { months: Number(params['months']) } : {})
      },
      { emitEvent: false }
    );

    this.loadDatabases();
    this.reload();

    this.filters.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
      const raw = this.filters.getRawValue();
      // Only a selection change writes the URL — never the constructor. See
      // syncFiltersToUrl's warning about NavigationSkipped and the sidebar.
      syncFiltersToUrl(this.router, this.route, {
        metric: raw.metric,
        databaseName: raw.databaseName,
        months: String(raw.months)
      });
      this.reload();
    });
  }

  protected reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const raw = this.filters.getRawValue();

    // One view, one error state: a half-rendered set of nine charts would be
    // worse than a single retry affordance.
    forkJoin({
      ranking: this.analyticsService.ranking(raw.metric),
      growth: forkJoin(
        Object.fromEntries(
          GROWTH_CHARTS.map((chart) => [
            chart.metric,
            this.analyticsService.growth(
              chart.metric,
              raw.months,
              raw.databaseName || undefined
            )
          ])
        )
      )
    }).subscribe({
      next: ({ ranking, growth }) => {
        this.rankingMetric.set(ranking.metric);
        this.ranking.set(ranking.entries);
        this.growth.set(
          Object.fromEntries(
            Object.entries(growth).map(([metric, result]) => [
              metric,
              // With a database selected there is exactly one series; without
              // one, the first is shown and the selector is how a reader picks.
              result.series[0]?.series ?? []
            ])
          )
        );
        this.loading.set(false);
        this.firstLoad.set(false);
      },
      error: (err) => {
        this.error.set(toViewError(err, this.i18n.translate('sacAnalytics.error')));
        this.loading.set(false);
        this.firstLoad.set(false);
      }
    });
  }

  protected pointsFor(metric: SacMetric): SacGrowthPoint[] {
    return this.growth()[metric] ?? [];
  }

  /** The database whose series are on screen — the selection, or whichever came first. */
  protected readonly shownDatabase = computed(() => {
    const selected = this.filters.getRawValue().databaseName;
    return selected || this.databases()[0] || '';
  });

  private loadDatabases(): void {
    this.analyticsService.databases().subscribe({
      next: (names) => this.databases.set(names),
      // A failed selector is not worth blocking the charts over; the charts'
      // own error state covers the case where the data itself is unreachable.
      error: () => this.databases.set([])
    });
  }
}
