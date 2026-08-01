import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AuditLog, AuditLogSchema } from './audit-log.schema';
import { AuditLogRepository } from './audit-log.repository';
import { AuditLogService } from './audit-log.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: AuditLog.name, schema: AuditLogSchema },
    ]),
  ],
  providers: [AuditLogRepository, AuditLogService],
  exports: [AuditLogService],
})
export class AuditLogModule {}
