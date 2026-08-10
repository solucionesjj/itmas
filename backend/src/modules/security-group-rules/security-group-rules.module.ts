import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import {
  SecurityGroupRule,
  SecurityGroupRuleSchema,
} from './security-group-rule.schema';
import { SecurityGroupRulesRepository } from './security-group-rules.repository';
import { SecurityGroupRulesService } from './security-group-rules.service';
import { SecurityGroupRulesController } from './security-group-rules.controller';
import { AuditLogModule } from '../audit-log/audit-log.module';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: SecurityGroupRule.name, schema: SecurityGroupRuleSchema },
    ]),
    AuditLogModule,
  ],
  controllers: [SecurityGroupRulesController],
  providers: [SecurityGroupRulesRepository, SecurityGroupRulesService],
  // Repository exported for security-group-sync (EXT-1.2) to write into,
  // same composition pattern as alert-engine importing alert-rules/alerts.
  exports: [SecurityGroupRulesRepository, SecurityGroupRulesService],
})
export class SecurityGroupRulesModule {}
