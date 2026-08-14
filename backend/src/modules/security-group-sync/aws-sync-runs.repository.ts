import { Injectable, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { ConfigService } from '@nestjs/config';
import { Model } from 'mongoose';
import { AwsSyncRun, AwsSyncRunDocument } from './aws-sync-run.schema';
import { AwsSyncRunStatus } from './aws-sync-run-status.enum';
import { ensureTtlIndex } from '../../common/mongo/ensure-ttl-index.util';

@Injectable()
export class AwsSyncRunsRepository implements OnModuleInit {
  constructor(
    @InjectModel(AwsSyncRun.name)
    private readonly runModel: Model<AwsSyncRunDocument>,
    private readonly configService: ConfigService,
  ) {}

  // Retention (ADR-0013): the run log is an operational artifact, not a
  // compliance record — the coarse fact still lands in audit_log, which has
  // its own, longer, independently configured retention.
  async onModuleInit(): Promise<void> {
    const days = this.configService.getOrThrow<number>('syncRunRetentionDays');
    await ensureTtlIndex(
      this.runModel.collection,
      'startedAt',
      days * 24 * 60 * 60,
    );
  }

  create(data: AwsSyncRun): Promise<AwsSyncRunDocument> {
    return this.runModel.create(data);
  }

  findRecent(limit: number): Promise<AwsSyncRunDocument[]> {
    return this.runModel.find().sort({ startedAt: -1 }).limit(limit).exec();
  }

  /**
   * Most recent run that actually observed something (RF-27's "since the
   * last run" baseline) — a total `failure` run (e.g. every region
   * unreachable) never counts, but `partial_failure` does, since it still
   * upserted whatever it could reach.
   */
  findLatestNonFailure(): Promise<AwsSyncRunDocument | null> {
    return this.runModel
      .findOne({ status: { $ne: AwsSyncRunStatus.FAILURE } })
      .sort({ startedAt: -1 })
      .exec();
  }
}
