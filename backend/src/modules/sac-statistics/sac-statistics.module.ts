import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { SacStatistic, SacStatisticSchema } from './sac-statistic.schema';
import { SacStatisticsRepository } from './sac-statistics.repository';
import { SacStatisticsService } from './sac-statistics.service';
import { SacAnalyticsService } from './sac-analytics.service';
import { SacStatisticsController } from './sac-statistics.controller';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: SacStatistic.name, schema: SacStatisticSchema },
    ]),
  ],
  controllers: [SacStatisticsController],
  // The service is exported so IngestionModule can reach it: the
  // node-authenticated write half of this resource lives in
  // IngestionController, alongside POST /inventory and POST /access-events,
  // rather than in a controller that also serves JWT routes (agent.md §5.4
  // forbids mixing the two auth mechanisms on one endpoint). The repository is
  // exported for the reports module's streaming export, and SacAnalyticsService
  // for StatsModule's /stats/sac endpoints — one data path onto this collection,
  // not a second query surface per consumer.
  providers: [
    SacStatisticsRepository,
    SacStatisticsService,
    SacAnalyticsService,
  ],
  exports: [SacStatisticsRepository, SacStatisticsService, SacAnalyticsService],
})
export class SacStatisticsModule {}
