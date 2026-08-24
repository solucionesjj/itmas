/** The nine metrics the analytics endpoints accept (BL-033 CA-3). */
export type SacMetric =
  | 'totalSizeGb'
  | 'activeAccounts'
  | 'debtors'
  | 'activities'
  | 'activitiesLast30Days'
  | 'avgActiveUsersLast3Months'
  | 'balanceSum'
  | 'overdueSum'
  | 'principalSum';

/**
 * A metric value on the wire: a decimal **string** for the three financial sums
 * — they are Decimal128 server-side, and a JSON number would lose cents — a
 * number for the integer gauges, and `null` for a month with no snapshot.
 */
export type SacMetricValue = string | number | null;

export interface SacRankingEntry {
  databaseName: string;
  value: SacMetricValue;
  generatedAt: string;
}

export interface SacRanking {
  metric: SacMetric;
  at: string | null;
  entries: SacRankingEntry[];
}

export interface SacGrowthPoint {
  /** `YYYY-MM`, UTC. */
  month: string;
  value: SacMetricValue;
  delta: SacMetricValue;
  deltaPercent: number | null;
}

export interface SacGrowthSeries {
  databaseName: string;
  series: SacGrowthPoint[];
}

export interface SacGrowth {
  metric: SacMetric;
  /** The dense month grid every series is aligned to, oldest first. */
  months: string[];
  series: SacGrowthSeries[];
}

/** Which figure a growth chart plots — see the note on the two activity counters. */
export type GrowthPlot = 'value' | 'delta';
