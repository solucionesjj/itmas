import { Injectable, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { ConfigService } from '@nestjs/config';
import { Model } from 'mongoose';
import { AccessEvent, AccessEventDocument } from './access-event.schema';
import { isDuplicateKeyError } from '../../common/mongo/duplicate-key.util';
import { ensureTtlIndex } from '../../common/mongo/ensure-ttl-index.util';

export interface InsertAccessEventResult {
  /** false when this exact natural key was already stored — an idempotent retry. */
  created: boolean;
}

@Injectable()
export class AccessEventsRepository implements OnModuleInit {
  constructor(
    @InjectModel(AccessEvent.name)
    private readonly accessEventModel: Model<AccessEventDocument>,
    private readonly configService: ConfigService,
  ) {}

  // Retention (agent.md §5.3/§9, Assumption #13).
  async onModuleInit(): Promise<void> {
    const days = this.configService.getOrThrow<number>(
      'retention.accessEventsDays',
    );
    await ensureTtlIndex(
      this.accessEventModel.collection,
      'timestamp',
      days * 24 * 60 * 60,
    );
  }

  async insert(data: AccessEvent): Promise<InsertAccessEventResult> {
    try {
      await this.accessEventModel.create(data);
      return { created: true };
    } catch (error) {
      if (isDuplicateKeyError(error)) {
        return { created: false };
      }
      throw error;
    }
  }
}
