import { Module } from '@nestjs/common';
import { DevicesModule } from '../devices/devices.module';
import { StatsController } from './stats.controller';
import { StatsService } from './stats.service';

@Module({
  imports: [DevicesModule],
  controllers: [StatsController],
  providers: [StatsService],
})
export class StatsModule {}
