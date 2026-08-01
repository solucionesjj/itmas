import { Component, computed, input } from '@angular/core';
import { OsStat } from './stats.model';

interface ChartRow {
  os: string;
  count: number;
  widthPercent: number;
  colorLight: string;
  colorDark: string;
}

// Neutral gray for the folded "Otros" bucket — deliberately NOT a categorical
// hue, since it doesn't represent one identity and must not collide with (or
// impersonate) any real category's assigned color.
const OTHER_COLOR = { light: '#898781', dark: '#898781' };

// Fixed-order validated categorical palette (light/dark) — assign in this exact
// order, never cycled or regenerated. Beyond 8 categories, fold the tail into
// "Other" rather than manufacture a 9th hue (breaks CVD separation guarantees).
const PALETTE: { light: string; dark: string }[] = [
  { light: '#2a78d6', dark: '#3987e5' }, // blue
  { light: '#eb6834', dark: '#d95926' }, // orange
  { light: '#1baf7a', dark: '#199e70' }, // aqua
  { light: '#eda100', dark: '#c98500' }, // yellow
  { light: '#e87ba4', dark: '#d55181' }, // magenta
  { light: '#008300', dark: '#008300' }, // green
  { light: '#4a3aa7', dark: '#9085e9' }, // violet
  { light: '#e34948', dark: '#e66767' } // red
];

@Component({
  selector: 'app-os-distribution-chart',
  standalone: true,
  templateUrl: './os-distribution-chart.component.html',
  styleUrl: './os-distribution-chart.component.scss'
})
export class OsDistributionChartComponent {
  readonly data = input<OsStat[]>([]);

  protected readonly rows = computed<ChartRow[]>(() => {
    const sorted = [...this.data()].sort((a, b) => b.count - a.count);
    const hasOverflow = sorted.length > PALETTE.length;
    // Reserve one slot for "Otros" when folding is needed, so it never reuses
    // (and is never confused for) the last real category's assigned hue.
    const headSize = hasOverflow ? PALETTE.length - 1 : PALETTE.length;

    const head = sorted.slice(0, headSize);
    const tail = sorted.slice(headSize);

    const withColor: { os: string; count: number; light: string; dark: string }[] =
      head.map((stat, index) => ({
        os: stat.os,
        count: stat.count,
        light: PALETTE[index].light,
        dark: PALETTE[index].dark
      }));

    if (tail.length > 0) {
      withColor.push({
        os: 'Otros',
        count: tail.reduce((sum, stat) => sum + stat.count, 0),
        light: OTHER_COLOR.light,
        dark: OTHER_COLOR.dark
      });
    }

    const max = Math.max(...withColor.map((row) => row.count), 1);
    return withColor.map((row) => ({
      os: row.os,
      count: row.count,
      widthPercent: (row.count / max) * 100,
      colorLight: row.light,
      colorDark: row.dark
    }));
  });

  // Screen-reader summary for the chart-as-image; the visually-hidden table
  // in the template carries the same data as real, navigable markup for
  // assistive tech that doesn't rely on the summary alone.
  protected readonly ariaSummary = computed(() => {
    const rows = this.rows();
    if (rows.length === 0) {
      return 'Distribución de sistemas operativos: sin datos disponibles.';
    }
    const parts = rows.map((row) => `${row.os} ${row.count}`).join(', ');
    return `Distribución de sistemas operativos: ${parts}.`;
  });
}
