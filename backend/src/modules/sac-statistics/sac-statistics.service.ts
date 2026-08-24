import { Injectable } from '@nestjs/common';
import { JsonLoggerService } from '../../common/logger/json-logger.service';
import {
  SacStatisticInput,
  SacStatisticsRepository,
} from './sac-statistics.repository';
import { SacStatisticIngestDto } from './dto/sac-statistic-ingest.dto';

export interface SacStatisticsIngestAck {
  /** How many records the request carried. */
  received: number;
  /** How many were stored. Equal to `received` unless the batch was partially rejected. */
  inserted: number;
}

@Injectable()
export class SacStatisticsService {
  constructor(
    private readonly repository: SacStatisticsRepository,
    private readonly logger: JsonLoggerService,
  ) {
    this.logger.setContext(SacStatisticsService.name);
  }

  /**
   * BL-031: stores one engine run's worth of per-database statistics.
   *
   * Nothing is written to `audit_log` here, deliberately and for the same reason
   * `POST /inventory` writes nothing: the actor is a node, not a user, and
   * audit_log's contract is "who did what" about people. Provenance is instead
   * carried on every record as `deviceId`.
   */
  async ingest(
    deviceId: string,
    records: SacStatisticIngestDto[],
  ): Promise<SacStatisticsIngestAck> {
    const documents: SacStatisticInput[] = records.map((record) => ({
      deviceId,
      databaseName: record.databaseName,
      generatedAt: new Date(record.generatedAt),
      totalSizeGb: record.totalSizeGb ?? null,
      debtors: record.debtors ?? null,
      debtorActiveAccounts: record.debtorActiveAccounts ?? null,
      activeAccounts: record.activeAccounts ?? null,
      avgActiveUsersLast3Months: record.avgActiveUsersLast3Months ?? null,
      activities: record.activities ?? null,
      activitiesLast30Days: record.activitiesLast30Days ?? null,
      activeUsers: record.activeUsers ?? null,
      logSizeGb: record.logSizeGb ?? null,
      dataSizeGb: record.dataSizeGb ?? null,
      balanceSum: record.balanceSum ?? null,
      overdueSum: record.overdueSum ?? null,
      principalSum: record.principalSum ?? null,
    }));

    const inserted = await this.repository.insertMany(documents);

    this.logger.log('SAC statistics ingested', {
      deviceId,
      received: records.length,
      inserted,
    });

    return { received: records.length, inserted };
  }
}
