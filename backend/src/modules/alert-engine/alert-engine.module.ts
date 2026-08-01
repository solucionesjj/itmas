import { Module } from '@nestjs/common';
import { AlertRulesModule } from '../alert-rules/alert-rules.module';
import { AlertsModule } from '../alerts/alerts.module';
import { AlertEngineService } from './alert-engine.service';

@Module({
  imports: [AlertRulesModule, AlertsModule],
  providers: [AlertEngineService],
  exports: [AlertEngineService],
})
export class AlertEngineModule {}
