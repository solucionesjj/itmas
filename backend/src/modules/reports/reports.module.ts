import { Module } from '@nestjs/common';
import { DevicesModule } from '../devices/devices.module';
import { AlertsModule } from '../alerts/alerts.module';
import { ReportsController } from './reports.controller';
import { ReportsService } from './reports.service';

@Module({
  imports: [DevicesModule, AlertsModule],
  controllers: [ReportsController],
  providers: [ReportsService],
})
export class ReportsModule {}
