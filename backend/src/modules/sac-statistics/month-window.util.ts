export interface MonthWindow {
  /** `YYYY-MM` labels, oldest first — the dense grid the growth pipeline projects onto. */
  monthKeys: string[];
  /** Midnight UTC on the first day of the oldest month in the window. */
  windowStart: Date;
}

/**
 * The last `months` calendar months **inclusive of the month `now` falls in**,
 * in UTC (ADR-0018 fixes the month boundary to UTC rather than a local zone, so
 * the same snapshot always lands in the same bucket wherever the API runs).
 *
 * These are calendar labels, not query results — computing them here and passing
 * them into the aggregation as constants is what lets the pipeline build a dense
 * month grid without a second round trip, while still doing all the actual
 * calculation in Mongo (BL-033 CA-8).
 */
export function buildMonthWindow(months: number, now: Date): MonthWindow {
  const year = now.getUTCFullYear();
  const month = now.getUTCMonth(); // 0-based

  // Date.UTC normalises a negative month index across the year boundary, so
  // `months = 18` in February correctly walks back to the previous year.
  const windowStart = new Date(Date.UTC(year, month - (months - 1), 1));

  const monthKeys: string[] = [];
  for (let offset = months - 1; offset >= 0; offset -= 1) {
    const point = new Date(Date.UTC(year, month - offset, 1));
    const label = `${point.getUTCFullYear()}-${String(
      point.getUTCMonth() + 1,
    ).padStart(2, '0')}`;
    monthKeys.push(label);
  }

  return { monthKeys, windowStart };
}
