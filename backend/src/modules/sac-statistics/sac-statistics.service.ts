import { Injectable } from '@nestjs/common';
import { JsonLoggerService } from '../../common/logger/json-logger.service';
import {
  PagedResult,
  SacStatisticInput,
  SacStatisticsFilter,
  SacStatisticsRepository,
} from './sac-statistics.repository';
import { SacStatisticIngestDto } from './dto/sac-statistic-ingest.dto';
import { QuerySacStatisticsDto } from './dto/query-sac-statistics.dto';
import {
  SacStatisticResponse,
  toSacStatisticResponse,
} from './sac-statistic-response.mapper';
import {
  SacStatisticSortField,
  SacStatisticSortOrder,
} from './sac-statistic-sort-field.enum';

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

  /** BL-032 CA-1: the standard paginated envelope, mapped through the response mapper. */
  async findAll(
    query: QuerySacStatisticsDto,
  ): Promise<PagedResult<SacStatisticResponse>> {
    const result = await this.repository.findPaged(
      this.toFilter(query),
      query.sort ?? SacStatisticSortField.GENERATED_AT,
      query.order ?? SacStatisticSortOrder.DESC,
      query.page ?? 1,
      query.limit ?? 20,
    );

    return { ...result, items: result.items.map(toSacStatisticResponse) };
  }

  private toFilter(query: QuerySacStatisticsDto): SacStatisticsFilter {
    return {
      databaseName: query.databaseName,
      from: query.from,
      to: query.to,
    };
  }
}
