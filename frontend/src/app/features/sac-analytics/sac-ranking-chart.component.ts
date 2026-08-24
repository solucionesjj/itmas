import { Component, computed, inject, input } from '@angular/core';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/t.pipe';
import { formatDecimal } from '../sac-statistics/decimal-format.util';
import { SacMetric, SacRankingEntry } from './sac-analytics.model';
import { isDecimalMetric, toChartNumber } from './metric-value.util';

interface RankRow {
  databaseName: string;
  /** Already formatted for the reader — exact for the decimal metrics. */
  label: string;
  widthPercent: number;
  colorVar: string;
}

/**
 * design.md §2.7's categorical slots, assigned in order (ADR-0009).
 *
 * Two are left out on purpose. Slot 8 is the reserved "other/unknown" neutral,
 * so a ranking longer than this list wraps back to slot 1 rather than borrowing
 * it and implying those rows are an aggregate. Slot 7 is red, which §2.7 notes
 * doubles as the compliance triad's failure colour — in a plain size ranking
 * that would read as "this database is in trouble", which is not what the bar
 * means.
 */
const SLOTS = [
  '--chart-1',
  '--chart-2',
  '--chart-3',
  '--chart-4',
  '--chart-5',
  '--chart-6'
] as const;

/** BL-033 indicator 1: databases ordered largest to smallest by one metric. */
@Component({
  selector: 'app-sac-ranking-chart',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './sac-ranking-chart.component.html',
  styleUrl: './sac-ranking-chart.component.scss'
})
export class SacRankingChartComponent {
  private readonly i18n = inject(I18nService);

  readonly entries = input<SacRankingEntry[]>([]);
  readonly metric = input<SacMetric>('totalSizeGb');

  protected readonly rows = computed<RankRow[]>(() => {
    const entries = this.entries();
    const numbers = entries.map((entry) => toChartNumber(entry.value) ?? 0);
    // Guarded against a max of 0: every bar would otherwise divide by zero and
    // render as NaN% wide, which paints nothing at all.
    const max = Math.max(...numbers, 1);

    return entries.map((entry, index) => ({
      databaseName: entry.databaseName,
      label: this.format(entry.value),
      widthPercent: ((numbers[index] ?? 0) / max) * 100,
      colorVar: SLOTS[index % SLOTS.length]
    }));
  });

  /** Screen-reader summary for the chart-as-image; the hidden table has the detail. */
  protected readonly ariaSummary = computed(() => {
    const rows = this.rows();
    if (rows.length === 0) {
      return this.i18n.translate('sacAnalytics.rankingEmptySummary');
    }
    return this.i18n.translate('sacAnalytics.rankingSummary', {
      parts: rows.map((row) => `${row.databaseName} ${row.label}`).join(', ')
    });
  });

  protected format(value: string | number | null): string {
    if (value === null) {
      return '—';
    }
    if (isDecimalMetric(this.metric())) {
      return formatDecimal(String(value), this.i18n.locale());
    }
    return new Intl.NumberFormat(this.i18n.locale()).format(Number(value));
  }
}
