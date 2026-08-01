import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AlertRule, AlertRuleSchema } from './alert-rule.schema';
import { AlertRulesRepository } from './alert-rules.repository';
import { AlertRulesService } from './alert-rules.service';
import { AlertRulesController } from './alert-rules.controller';
import { AuditLogModule } from '../audit-log/audit-log.module';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: AlertRule.name, schema: AlertRuleSchema },
    ]),
    AuditLogModule,
  ],
  controllers: [AlertRulesController],
  providers: [AlertRulesRepository, AlertRulesService],
  exports: [AlertRulesRepository, AlertRulesService],
})
export class AlertRulesModule {}
