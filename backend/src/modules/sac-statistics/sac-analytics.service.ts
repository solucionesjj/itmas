import { Injectable } from '@nestjs/common';
import { Types } from 'mongoose';
import { SacStatisticsRepository } from './sac-statistics.repository';
import { SacMetric, isDecimalMetric } from './sac-metric.enum';
import { buildMonthWindow } from './month-window.util';
import { QuerySacRankingDto } from './dto/query-sac-ranking.dto';
import { QuerySacGrowthDto } from './dto/query-sac-growth.dto';

/**
 * A metric value on the wire: a decimal **string** for the three financial sums
 * (they are Decimal128 and would lose cents as a JSON number), a number for the
 * integer gauges, `null` when the snapshot did not carry it.
 */
export type SacMetricValue = string | number | null;

export interface SacRankingEntry {
  databaseName: string;
  value: SacMetricValue;
  /** The snapshot this row was taken from, so the reader knows how fresh it is. */
  generatedAt: string;
}

export interface SacRankingResponse {
  metric: SacMetric;
  /** Echoed back so a client can tell an as-of query from a latest query. */
  at: string | null;
  entries: SacRankingEntry[];
}

export interface SacGrowthPointResponse {
  /** `YYYY-MM`, UTC. */
  month: string;
  /** `null` means no snapshot for that month — never interpolated (CA-5). */
  value: SacMetricValue;
  /** Absolute variation against the previous calendar month. */
  delta: SacMetricValue;
  /** Percentage variation, or null when either month is missing or the base is 0. */
  deltaPercent: number | null;
}

export interface SacGrowthSeriesResponse {
  databaseName: string;
  series: SacGrowthPointResponse[];
}

export interface SacGrowthResponse {
  metric: SacMetric;
  /** The dense month grid the series are aligned to, oldest first. */
  months: string[];
  series: SacGrowthSeriesResponse[];
}

@Injectable()
export class SacAnalyticsService {
  constructor(private readonly repository: SacStatisticsRepository) {}

  /** BL-033 indicator 1. */
  async ranking(query: QuerySacRankingDto): Promise<SacRankingResponse> {
    const at = query.at ? new Date(query.at) : undefined;
    const rows = await this.repository.rankByMetric(query.metric, at);

    return {
      metric: query.metric,
      at: query.at ?? null,
      entries: rows.map((row) => ({
        databaseName: row.databaseName,
        value: this.toWireValue(query.metric, row.value),
        generatedAt: row.generatedAt.toISOString(),
      })),
    };
  }

  /**
   * BL-033 indicators 2–7.
   *
   * The month grid is built here and passed into the aggregation as constants —
   * calendar labels, not data — which is what lets the pipeline densify the
   * sparse per-month results and still do every calculation in Mongo (CA-8).
   *
   * On the two activity counters (CA-6): both are cumulative, and the uniform
   * `{value, delta, deltaPercent}` shape serves both, but they are READ
   * differently and must not share an axis. `activities` never resets, so the
   * meaningful monthly figure is `delta` — the activities performed that month.
   * `activitiesLast30Days` is a fixed 30-day window and therefore already
   * comparable month to month, so the meaningful figure is `value` itself.
   */
  async growth(query: QuerySacGrowthDto): Promise<SacGrowthResponse> {
    const months = query.months ?? 12;
    const { monthKeys, windowStart } = buildMonthWindow(months, new Date());

    const series = await this.repository.growthByMetric(
      query.metric,
      monthKeys,
      windowStart,
      query.databaseName,
    );

    return {
      metric: query.metric,
      months: monthKeys,
      series: series.map((entry) => ({
        databaseName: entry.databaseName,
        series: entry.series.map((point) => ({
          month: point.month,
          value: this.toWireValue(query.metric, point.value),
          delta: this.toWireValue(query.metric, point.delta),
          deltaPercent: point.deltaPercent ?? null,
        })),
      })),
    };
  }

  /** Distinct database names, for the analytics view's selector. */
  listDatabaseNames(): Promise<string[]> {
    return this.repository.listDatabaseNames();
  }

  /**
   * Decimal128 stringifies as `{"$numberDecimal": …}` through JSON.stringify, a
   * BSON internal rather than a contract — so the three financial metrics are
   * converted explicitly, exactly as the record mapper does.
   */
  private toWireValue(
    metric: SacMetric,
    value: Types.Decimal128 | number | null | undefined,
  ): SacMetricValue {
    if (value === null || value === undefined) {
      return null;
    }
    if (isDecimalMetric(metric)) {
      return typeof value === 'number' ? String(value) : value.toString();
    }
    return typeof value === 'number' ? value : Number(value.toString());
  }
}
