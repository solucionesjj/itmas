import { Module } from '@nestjs/common';
import { DevicesModule } from '../devices/devices.module';
import { SacStatisticsModule } from '../sac-statistics/sac-statistics.module';
import { StatsController } from './stats.controller';
import { StatsService } from './stats.service';

@Module({
  imports: [DevicesModule, SacStatisticsModule],
  controllers: [StatsController],
  providers: [StatsService],
})
export class StatsModule {}
