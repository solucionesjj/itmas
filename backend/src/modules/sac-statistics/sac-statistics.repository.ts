import { Injectable, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { ConfigService } from '@nestjs/config';
import { Model, PipelineStage, Types } from 'mongoose';
import { SacStatistic, SacStatisticDocument } from './sac-statistic.schema';
import { SacMetric, metricExpression } from './sac-metric.enum';
import {
  SacStatisticSortField,
  SacStatisticSortOrder,
} from './sac-statistic-sort-field.enum';
import { escapeRegex } from '../../common/util/escape-regex.util';
import { ensureTtlIndex } from '../../common/mongo/ensure-ttl-index.util';

/**
 * Insert shape for the three financial sums: decimal **strings**, which the
 * Decimal128 SchemaType parses exactly. Distinct from the stored
 * `SacStatistic` (where they are Decimal128 instances) on purpose — handing
 * Mongoose a JS number here would reintroduce the double rounding the
 * Decimal128 column exists to prevent, and the type is what stops that.
 */
export type SacStatisticInput = Omit<
  SacStatistic,
  'balanceSum' | 'overdueSum' | 'principalSum'
> & {
  balanceSum: string | null;
  overdueSum: string | null;
  principalSum: string | null;
};

export interface SacStatisticsFilter {
  databaseName?: string;
  from?: string;
  to?: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

/** One row of `GET /stats/sac/ranking`. */
export interface SacRankingRow {
  databaseName: string;
  /** Decimal128 for the three financial metrics, number otherwise, null if the snapshot lacks it. */
  value: Types.Decimal128 | number | null;
  /** The `generatedAt` of the snapshot this row was taken from. */
  generatedAt: Date;
}

export interface SacGrowthPoint {
  /** `YYYY-MM`, UTC. */
  month: string;
  value: Types.Decimal128 | number | null;
  delta: Types.Decimal128 | number | null;
  deltaPercent: number | null;
}

export interface SacGrowthSeries {
  databaseName: string;
  series: SacGrowthPoint[];
}

@Injectable()
export class SacStatisticsRepository implements OnModuleInit {
  constructor(
    @InjectModel(SacStatistic.name)
    private readonly model: Model<SacStatisticDocument>,
    private readonly configService: ConfigService,
  ) {}

  /**
   * Unlike inventories/access_events/audit_log, this collection has NO default
   * TTL (BL-031 CA-7): year-over-year growth analysis needs the long history,
   * and a silent 180-day purge would quietly make that analysis wrong. A TTL is
   * only created when an operator explicitly sets SAC_STATISTICS_RETENTION_DAYS.
   *
   * Note there is no "un-TTL" path: dropping the variable again leaves the
   * existing index in place, since ensureTtlIndex only ever creates or
   * recreates. Removing retention is a deliberate, documented DBA action
   * (DEPLOYMENT.md), not something a missing env var should do implicitly.
   */
  async onModuleInit(): Promise<void> {
    const days = this.configService.get<number | null>(
      'retention.sacStatisticsDays',
    );
    if (days === null || days === undefined) {
      return;
    }
    await ensureTtlIndex(
      this.model.collection,
      'generatedAt',
      days * 24 * 60 * 60,
    );
  }

  /**
   * Append-only bulk insert (BL-031 CA-2/CA-5). One engine run reports all its
   * databases in a single call, so this is one round trip rather than N.
   * `ordered: false` lets a single rejected record fail without discarding the
   * rest of the batch; there is no unique natural key, so a resend inserts
   * again by design rather than being absorbed as an idempotent retry.
   */
  async insertMany(records: SacStatisticInput[]): Promise<number> {
    if (records.length === 0) {
      return 0;
    }
    const inserted = await this.model.insertMany(records, { ordered: false });
    return inserted.length;
  }

  /**
   * Only ever populated from validated, whitelisted DTO fields — never raw
   * client keys — to keep NoSQL operator injection out (agent.md §6.7). The
   * free-text database name is regex-escaped before it reaches `$regex`, so a
   * value like `.*(a+)+` is matched literally instead of becoming a ReDoS.
   */
  private buildQuery(filter: SacStatisticsFilter): Record<string, unknown> {
    const query: Record<string, unknown> = {};

    if (filter.databaseName) {
      query.databaseName = {
        $regex: escapeRegex(filter.databaseName),
        $options: 'i',
      };
    }

    if (filter.from || filter.to) {
      const generatedAt: Record<string, Date> = {};
      if (filter.from) generatedAt.$gte = new Date(filter.from);
      if (filter.to) generatedAt.$lte = new Date(filter.to);
      query.generatedAt = generatedAt;
    }

    return query;
  }

  async findPaged(
    filter: SacStatisticsFilter,
    sort: SacStatisticSortField,
    order: SacStatisticSortOrder,
    page: number,
    limit: number,
  ): Promise<PagedResult<SacStatisticDocument>> {
    const query = this.buildQuery(filter);
    const direction = order === SacStatisticSortOrder.ASC ? 1 : -1;
    // `generatedAt` as the tie-break keeps paging stable when the primary sort
    // key repeats — without it, two pages can show the same document.
    const sortSpec: Record<string, 1 | -1> =
      sort === SacStatisticSortField.GENERATED_AT
        ? { generatedAt: direction, _id: 1 }
        : { [sort]: direction, generatedAt: -1, _id: 1 };

    const [items, total] = await Promise.all([
      this.model
        .find(query)
        .sort(sortSpec)
        .skip((page - 1) * limit)
        .limit(limit)
        .exec(),
      this.model.countDocuments(query).exec(),
    ]);

    return { items, total, page, limit };
  }

  /**
   * Export cursor (BL-032 CA-6): the FULL filtered set, newest first, read in
   * batches instead of materialised as an array. This collection grows daily and
   * has no default TTL, so — unlike `devices`/`alerts`, which are bounded by the
   * size of the estate — the unfiltered history has no natural ceiling and a
   * `.find().exec()` here would eventually be an out-of-memory export.
   * `.lean()` skips hydrating a Mongoose document per row, which matters at
   * this size; the caller must therefore expect plain objects.
   */
  streamFiltered(filter: SacStatisticsFilter, batchSize = 500) {
    return this.model
      .find(this.buildQuery(filter))
      .sort({ generatedAt: -1, _id: 1 })
      .lean()
      .batchSize(batchSize)
      .cursor();
  }

  /**
   * BL-033 indicator 1: every database ranked by `metric`, using the LAST
   * snapshot of each database at (or before) `at` — the most recent one overall
   * when `at` is omitted. Nulls land at the end: Mongo sorts null/missing below
   * every number, so a descending sort puts a database without the metric last
   * rather than first.
   */
  async rankByMetric(metric: SacMetric, at?: Date): Promise<SacRankingRow[]> {
    const pipeline: PipelineStage[] = [];
    if (at) {
      pipeline.push({ $match: { generatedAt: { $lte: at } } });
    }
    pipeline.push(
      { $sort: { generatedAt: -1 } },
      {
        $group: {
          _id: '$databaseName',
          value: { $first: metricExpression(metric) },
          generatedAt: { $first: '$generatedAt' },
        },
      },
      // `_id` as the tie-break so equal values order predictably by name.
      { $sort: { value: -1, _id: 1 } },
      { $project: { _id: 0, databaseName: '$_id', value: 1, generatedAt: 1 } },
    );

    return this.model.aggregate<SacRankingRow>(pipeline).exec();
  }

  /**
   * BL-033 indicators 2–7: per database and per month, the value, the absolute
   * variation and the percentage variation against the previous month.
   *
   * Every step happens inside the aggregation (CA-8) — the service supplies only
   * the month *labels*, which are calendar constants, not data. Two things this
   * pipeline gets right that a naive version does not:
   *
   *  - **The month is the LAST snapshot of that month** (CA-4): `$sort` by
   *    `generatedAt` descending then `$first` per group. That defines the monthly
   *    grain and simultaneously collapses the duplicates BL-031's append-only
   *    model allows — two snapshots on the same day resolve to the later one.
   *  - **A month with no snapshot is a hole, not a bridge** (CA-5). The sparse
   *    per-month results are projected onto a DENSE grid of the requested months
   *    first, so "previous month" means the previous *calendar* month. Computing
   *    deltas over the compacted array instead would silently compare across a
   *    gap and report a two-month jump as one month's growth.
   */
  async growthByMetric(
    metric: SacMetric,
    monthKeys: string[],
    windowStart: Date,
    databaseName?: string,
  ): Promise<SacGrowthSeries[]> {
    const match: Record<string, unknown> = {
      generatedAt: { $gte: windowStart },
    };
    if (databaseName) {
      match.databaseName = {
        $regex: escapeRegex(databaseName),
        $options: 'i',
      };
    }

    const pipeline: PipelineStage[] = [
      { $match: match },
      { $sort: { generatedAt: -1 } },
      {
        $group: {
          _id: {
            databaseName: '$databaseName',
            // UTC by decision, not by omission: the month boundary is fixed to
            // UTC so the same snapshot always lands in the same bucket
            // regardless of where the API runs (ADR-0018).
            month: {
              $dateToString: {
                format: '%Y-%m',
                date: '$generatedAt',
                timezone: 'UTC',
              },
            },
          },
          value: { $first: metricExpression(metric) },
        },
      },
      {
        $group: {
          _id: '$_id.databaseName',
          points: { $push: { month: '$_id.month', value: '$value' } },
        },
      },
      {
        // Sparse months → dense grid, holes as explicit nulls.
        $addFields: {
          series: {
            $map: {
              input: monthKeys,
              as: 'm',
              in: {
                $let: {
                  vars: {
                    hit: {
                      $first: {
                        $filter: {
                          input: '$points',
                          as: 'p',
                          cond: { $eq: ['$$p.month', '$$m'] },
                        },
                      },
                    },
                  },
                  in: {
                    month: '$$m',
                    value: { $ifNull: ['$$hit.value', null] },
                  },
                },
              },
            },
          },
        },
      },
      {
        // Pair each month with the one immediately before it in the grid.
        $addFields: {
          series: {
            $map: {
              input: { $range: [0, { $size: '$series' }] },
              as: 'i',
              in: {
                $let: {
                  vars: {
                    cur: { $arrayElemAt: ['$series', '$$i'] },
                    prev: {
                      $cond: [
                        { $gt: ['$$i', 0] },
                        {
                          $arrayElemAt: ['$series', { $subtract: ['$$i', 1] }],
                        },
                        null,
                      ],
                    },
                  },
                  in: {
                    month: '$$cur.month',
                    value: '$$cur.value',
                    // Either side missing ⇒ no variation to report, rather than
                    // treating an absent month as a zero.
                    delta: {
                      $cond: [
                        {
                          $and: [
                            { $ne: ['$$cur.value', null] },
                            { $ne: ['$$prev.value', null] },
                          ],
                        },
                        { $subtract: ['$$cur.value', '$$prev.value'] },
                        null,
                      ],
                    },
                    // A percentage against a zero base is undefined, not
                    // infinite — reported as no data.
                    deltaPercent: {
                      $cond: [
                        {
                          $and: [
                            { $ne: ['$$cur.value', null] },
                            { $ne: ['$$prev.value', null] },
                            { $ne: ['$$prev.value', 0] },
                          ],
                        },
                        {
                          $round: [
                            {
                              $multiply: [
                                {
                                  $divide: [
                                    {
                                      $toDouble: {
                                        $subtract: [
                                          '$$cur.value',
                                          '$$prev.value',
                                        ],
                                      },
                                    },
                                    { $toDouble: '$$prev.value' },
                                  ],
                                },
                                100,
                              ],
                            },
                            2,
                          ],
                        },
                        null,
                      ],
                    },
                  },
                },
              },
            },
          },
        },
      },
      { $project: { _id: 0, databaseName: '$_id', series: 1 } },
      { $sort: { databaseName: 1 } },
    ];

    return this.model.aggregate<SacGrowthSeries>(pipeline).exec();
  }

  /** Distinct database names for the analytics view's selector, alphabetical. */
  async listDatabaseNames(): Promise<string[]> {
    const rows = await this.model
      .aggregate<{ _id: string }>([
        { $group: { _id: '$databaseName' } },
        { $sort: { _id: 1 } },
      ])
      .exec();
    return rows.map((row) => row._id);
  }
}
