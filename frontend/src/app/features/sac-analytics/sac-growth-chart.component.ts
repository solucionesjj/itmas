import { Component, computed, inject, input } from '@angular/core';
import { I18nService } from '../../core/i18n/i18n.service';
import { MessageKey } from '../../core/i18n/messages.es-CO';
import { TranslatePipe } from '../../core/i18n/t.pipe';
import { formatDecimal } from '../sac-statistics/decimal-format.util';
import { GrowthPlot, SacGrowthPoint, SacMetric } from './sac-analytics.model';
import { isDecimalMetric, toChartNumber } from './metric-value.util';

interface Column {
  month: string;
  /** Short month label for the axis, e.g. "ago 26". */
  monthLabel: string;
  /** null = no snapshot that month. Rendered as a gap, never as zero. */
  value: number | null;
  valueLabel: string;
  deltaLabel: string;
  /** 'up' | 'down' | 'none' — drives the delta colour token. */
  trend: 'up' | 'down' | 'none';
  /** Percent of the plot area this column's bar occupies. */
  heightPercent: number;
  /** Distance from the plot floor to the bar's base, as a percent. */
  bottomPercent: number;
  negative: boolean;
}

/**
 * One metric's month-over-month series for a single database, drawn as columns
 * without a charting library (BL-033 CA-10) — same technique as the OS
 * distribution chart, rotated.
 */
@Component({
  selector: 'app-sac-growth-chart',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './sac-growth-chart.component.html',
  styleUrl: './sac-growth-chart.component.scss'
})
export class SacGrowthChartComponent {
  private readonly i18n = inject(I18nService);

  readonly points = input<SacGrowthPoint[]>([]);
  readonly metric = input<SacMetric>('totalSizeGb');
  /** Which figure to draw — see the note in the analytics component on `activities`. */
  readonly plot = input<GrowthPlot>('value');
  /** The chart's own heading, as a message key. */
  readonly titleKey = input.required<MessageKey>();
  /** One-line explanation of what the columns mean, as a message key. */
  readonly captionKey = input.required<MessageKey>();
  readonly colorVar = input('--chart-1');

  protected readonly columns = computed<Column[]>(() => {
    const points = this.points();
    const plot = this.plot();
    const values = points.map((point) =>
      toChartNumber(plot === 'value' ? point.value : point.delta)
    );
    const present = values.filter((value): value is number => value !== null);

    // The axis spans the data, plus zero whenever the series crosses it — so a
    // set of columns that are all, say, 590–660 does not exaggerate a 10%
    // difference into a bar ten times taller than its neighbour.
    const max = Math.max(0, ...present);
    const min = Math.min(0, ...present);
    const span = max - min || 1;
    // Where zero sits, measured up from the plot floor.
    const zeroPercent = ((0 - min) / span) * 100;

    return points.map((point, index) => {
      const value = values[index];
      const negative = value !== null && value < 0;
      const magnitude = value === null ? 0 : (Math.abs(value) / span) * 100;

      return {
        month: point.month,
        monthLabel: this.monthLabel(point.month),
        value,
        valueLabel: this.format(plot === 'value' ? point.value : point.delta),
        deltaLabel:
          point.deltaPercent === null
            ? '—'
            : `${point.deltaPercent > 0 ? '+' : ''}${new Intl.NumberFormat(
                this.i18n.locale(),
                { maximumFractionDigits: 1 }
              ).format(point.deltaPercent)}%`,
        trend:
          point.deltaPercent === null || point.deltaPercent === 0
            ? 'none'
            : point.deltaPercent > 0
              ? 'up'
              : 'down',
        heightPercent: magnitude,
        bottomPercent: negative ? zeroPercent - magnitude : zeroPercent,
        negative
      };
    });
  });

  /** True when the series crosses zero and the baseline has to be drawn. */
  protected readonly hasNegative = computed(() =>
    this.columns().some((column) => column.negative)
  );

  protected readonly zeroLinePercent = computed(() => {
    const first = this.columns().find((column) => !column.negative);
    return first?.bottomPercent ?? 0;
  });

  protected readonly hasData = computed(() =>
    this.columns().some((column) => column.value !== null)
  );

  /** Screen-reader summary; the hidden table below carries every figure. */
  protected readonly ariaSummary = computed(() => {
    const withData = this.columns().filter((column) => column.value !== null);
    if (withData.length === 0) {
      return this.i18n.translate('sacAnalytics.growthEmptySummary', {
        title: this.i18n.translate(this.titleKey())
      });
    }
    return this.i18n.translate('sacAnalytics.growthSummary', {
      title: this.i18n.translate(this.titleKey()),
      parts: withData
        .map((column) => `${column.monthLabel} ${column.valueLabel}`)
        .join(', ')
    });
  });

  /**
   * `2026-08` → a compact numeric label like `08/26`.
   *
   * Deliberately numeric rather than `{month: 'short'}`: es-CO renders that as
   * "sept de 25", which at twelve columns inside a 320px card overlaps its
   * neighbours into an unreadable smear. The full `YYYY-MM` is still in the
   * column's tooltip and in the accessible table, so nothing is lost.
   */
  private monthLabel(month: string): string {
    const [year, monthNumber] = month.split('-').map(Number);
    return new Intl.DateTimeFormat(this.i18n.locale(), {
      month: '2-digit',
      year: '2-digit',
      timeZone: 'UTC'
    }).format(new Date(Date.UTC(year, monthNumber - 1, 1)));
  }

  protected format(value: string | number | null): string {
    if (value === null) {
      return this.i18n.translate('sacAnalytics.noData');
    }
    if (isDecimalMetric(this.metric())) {
      return formatDecimal(String(value), this.i18n.locale());
    }
    return new Intl.NumberFormat(this.i18n.locale()).format(Number(value));
  }
}
