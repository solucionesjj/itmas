import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AwsSyncRun, AwsSyncRunSchema } from './aws-sync-run.schema';
import { AwsSyncRunsRepository } from './aws-sync-runs.repository';
import { AwsEc2ClientFactory } from './aws-ec2-client.factory';
import { SecurityGroupSyncService } from './security-group-sync.service';
import { SecurityGroupSyncController } from './security-group-sync.controller';
import { SecurityGroupRulesModule } from '../security-group-rules/security-group-rules.module';
import { AuditLogModule } from '../audit-log/audit-log.module';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: AwsSyncRun.name, schema: AwsSyncRunSchema },
    ]),
    // Composition pattern from alert-engine importing alert-rules/alerts —
    // this module writes into security-group-rules' collection via its
    // exported repository, rather than a second query path onto the same data.
    SecurityGroupRulesModule,
    AuditLogModule,
  ],
  controllers: [SecurityGroupSyncController],
  providers: [
    AwsEc2ClientFactory,
    AwsSyncRunsRepository,
    SecurityGroupSyncService,
  ],
  exports: [SecurityGroupSyncService],
})
export class SecurityGroupSyncModule {}
