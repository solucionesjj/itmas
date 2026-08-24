import { Injectable, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { ConfigService } from '@nestjs/config';
import { Model } from 'mongoose';
import { SacStatistic, SacStatisticDocument } from './sac-statistic.schema';
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
}
