import { SacMetricValue } from './sac-analytics.model';

/**
 * A metric value as a JS number, for **chart geometry only**.
 *
 * The financial sums arrive as exact decimal strings and must stay exact
 * wherever they are read as figures (see `formatDecimal`). A bar's height, on
 * the other hand, is a handful of pixels: converting to a double there loses
 * nothing a screen could show. Keeping the two uses in separate functions is
 * what stops the convenient one leaking into the column that has to be right.
 */
export function toChartNumber(value: SacMetricValue): number | null {
  if (value === null) {
    return null;
  }
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}

/** True for the three metrics whose values are exact decimal strings. */
export function isDecimalMetric(metric: string): boolean {
  return metric === 'balanceSum' || metric === 'overdueSum' || metric === 'principalSum';
}
