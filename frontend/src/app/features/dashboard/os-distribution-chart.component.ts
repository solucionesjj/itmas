import { Component, computed, inject, input } from '@angular/core';
import { I18nService } from '../../core/i18n/i18n.service';
import { TranslatePipe } from '../../core/i18n/t.pipe';
import { OsStat } from './stats.model';

interface ChartRow {
  os: string;
  count: number;
  widthPercent: number;
  /** The CSS custom property holding this row's slot colour. */
  colorVar: string;
}

/**
 * The eight fixed categorical slots of design.md §2.7, assigned in this exact
 * order and never cycled or regenerated (ADR-0009). Each token already carries
 * its own light and dark value, so nothing here knows a hex and the bars follow
 * the theme without a second variable per row.
 *
 * §2.7 notes that slots 5/6/7 are *also* the compliance triad; in a plain
 * categorical series like this one they are simply slots 5, 6 and 7.
 */
const SLOTS = [
  '--chart-1',
  '--chart-2',
  '--chart-3',
  '--chart-4',
  '--chart-5',
  '--chart-6',
  '--chart-7'
] as const;

/**
 * Slot 8 is §2.7's "Other / unknown" and is reserved for the folded bucket, so
 * it can never collide with — or impersonate — a real category's colour. That is
 * why SLOTS above stops at seven.
 */
const OTHER_SLOT = '--chart-8';

@Component({
  selector: 'app-os-distribution-chart',
  standalone: true,
  imports: [TranslatePipe],
  templateUrl: './os-distribution-chart.component.html',
  styleUrl: './os-distribution-chart.component.scss'
})
export class OsDistributionChartComponent {
  private readonly i18n = inject(I18nService);

  readonly data = input<OsStat[]>([]);

  protected readonly rows = computed<ChartRow[]>(() => {
    const sorted = [...this.data()].sort((a, b) => b.count - a.count);

    const head = sorted.slice(0, SLOTS.length);
    const tail = sorted.slice(SLOTS.length);

    // Typed explicitly: `SLOTS` is `as const`, so an inferred element type would
    // exclude the OTHER_SLOT pushed below.
    const withColor: { os: string; count: number; colorVar: string }[] = head.map(
      (stat, index) => ({
        os: stat.os,
        count: stat.count,
        colorVar: SLOTS[index]
      })
    );

    // A ninth category folds into a neutral "Otros" bucket rather than
    // manufacturing a ninth hue, which would break the palette's separation
    // guarantees (ADR-0009).
    if (tail.length > 0) {
      withColor.push({
        os: this.i18n.translate('dashboard.chartOther'),
        count: tail.reduce((sum, stat) => sum + stat.count, 0),
        colorVar: OTHER_SLOT
      });
    }

    const max = Math.max(...withColor.map((row) => row.count), 1);
    return withColor.map((row) => ({
      os: row.os,
      count: row.count,
      widthPercent: (row.count / max) * 100,
      colorVar: row.colorVar
    }));
  });

  // Screen-reader summary for the chart-as-image; the visually-hidden table
  // in the template carries the same data as real, navigable markup for
  // assistive tech that doesn't rely on the summary alone.
  protected readonly ariaSummary = computed(() => {
    const rows = this.rows();
    if (rows.length === 0) {
      return this.i18n.translate('dashboard.chartEmptySummary');
    }
    const parts = rows.map((row) => `${row.os} ${row.count}`).join(', ');
    return this.i18n.translate('dashboard.chartSummary', { parts });
  });
}
