/**
 * The closed whitelist of metrics the BL-033 analytics endpoints accept: the
 * eight that feed the required indicators plus `activitiesLast30Days`. A value
 * outside this enum is a 400 — the metric name selects a pre-built expression
 * from SAC_METRIC_EXPRESSIONS below and is NEVER interpolated into an
 * aggregation stage.
 */
export enum SacMetric {
  TOTAL_SIZE_GB = 'totalSizeGb',
  ACTIVE_ACCOUNTS = 'activeAccounts',
  DEBTORS = 'debtors',
  ACTIVITIES = 'activities',
  ACTIVITIES_LAST_30_DAYS = 'activitiesLast30Days',
  AVG_ACTIVE_USERS_LAST_3_MONTHS = 'avgActiveUsersLast3Months',
  BALANCE_SUM = 'balanceSum',
  OVERDUE_SUM = 'overdueSum',
  PRINCIPAL_SUM = 'principalSum',
}

/**
 * The three Decimal128 sums. Aggregated with `$toDecimal` rather than in
 * floating point (BL-033 CA-7), so a monthly total or delta never drifts by
 * cents the way a double would at 18 significant digits.
 */
export const DECIMAL_METRICS: readonly SacMetric[] = [
  SacMetric.BALANCE_SUM,
  SacMetric.OVERDUE_SUM,
  SacMetric.PRINCIPAL_SUM,
];

export function isDecimalMetric(metric: SacMetric): boolean {
  return DECIMAL_METRICS.includes(metric);
}

/**
 * metric → the aggregation expression that reads it. Decimal metrics are cast
 * with `$toDecimal`; integer gauges are read directly. Going through this map
 * is what keeps the field path out of reach of client input.
 */
export function metricExpression(metric: SacMetric): unknown {
  return isDecimalMetric(metric) ? { $toDecimal: `$${metric}` } : `$${metric}`;
}
