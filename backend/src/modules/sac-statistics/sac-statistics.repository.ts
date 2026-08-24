import { Injectable, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { ConfigService } from '@nestjs/config';
import { Model } from 'mongoose';
import { SacStatistic, SacStatisticDocument } from './sac-statistic.schema';
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
}
