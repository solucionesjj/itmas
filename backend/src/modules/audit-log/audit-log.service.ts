import { Injectable } from '@nestjs/common';
import { Types } from 'mongoose';
import { AuditLogRepository } from './audit-log.repository';
import { AuditLogAction } from './audit-log.schema';

@Injectable()
export class AuditLogService {
  constructor(private readonly auditLogRepository: AuditLogRepository) {}

  async record(
    action: AuditLogAction,
    actorId?: string | Types.ObjectId,
    target?: string,
    detail?: Record<string, unknown>,
  ): Promise<void> {
    // detail must never contain passwords, tokens, or passwordHash values.
    await this.auditLogRepository.create({
      action,
      actorId: actorId ? new Types.ObjectId(actorId) : undefined,
      target,
      detail,
      timestamp: new Date(),
    });
  }
}
