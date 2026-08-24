import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { SacStatistic, SacStatisticSchema } from './sac-statistic.schema';
import { SacStatisticsRepository } from './sac-statistics.repository';
import { SacStatisticsService } from './sac-statistics.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: SacStatistic.name, schema: SacStatisticSchema },
    ]),
  ],
  // The service is exported so IngestionModule can reach it: the
  // node-authenticated write half of this resource lives in
  // IngestionController, alongside POST /inventory and POST /access-events,
  // rather than in a controller that also serves JWT routes (agent.md §5.4
  // forbids mixing the two auth mechanisms on one endpoint).
  providers: [SacStatisticsRepository, SacStatisticsService],
  exports: [SacStatisticsRepository, SacStatisticsService],
})
export class SacStatisticsModule {}
