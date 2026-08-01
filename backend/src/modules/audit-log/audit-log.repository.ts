import { Injectable, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { ConfigService } from '@nestjs/config';
import { Model } from 'mongoose';
import { AuditLog, AuditLogDocument } from './audit-log.schema';
import { ensureTtlIndex } from '../../common/mongo/ensure-ttl-index.util';

@Injectable()
export class AuditLogRepository implements OnModuleInit {
  constructor(
    @InjectModel(AuditLog.name)
    private readonly auditLogModel: Model<AuditLogDocument>,
    private readonly configService: ConfigService,
  ) {}

  // Retention (agent.md §5.3/§9, Assumption #13).
  async onModuleInit(): Promise<void> {
    const days = this.configService.getOrThrow<number>(
      'retention.auditLogDays',
    );
    await ensureTtlIndex(
      this.auditLogModel.collection,
      'timestamp',
      days * 24 * 60 * 60,
    );
  }

  create(entry: Partial<AuditLog>): Promise<AuditLogDocument> {
    return this.auditLogModel.create(entry);
  }
}
