import { Injectable, OnModuleInit } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { ConfigService } from '@nestjs/config';
import { Model } from 'mongoose';
import { Inventory, InventoryDocument } from './inventory.schema';
import { isDuplicateKeyError } from '../../common/mongo/duplicate-key.util';
import { ensureTtlIndex } from '../../common/mongo/ensure-ttl-index.util';

export interface InsertInventoryResult {
  /** false when this exact (deviceId, timestamp) was already stored — an idempotent retry. */
  created: boolean;
}

@Injectable()
export class InventoriesRepository implements OnModuleInit {
  constructor(
    @InjectModel(Inventory.name)
    private readonly inventoryModel: Model<InventoryDocument>,
    private readonly configService: ConfigService,
  ) {}

  // Retention (agent.md §5.3/§9, Assumption #13): purge inventories older
  // than the configured window via a TTL index, not an ad-hoc purge job.
  async onModuleInit(): Promise<void> {
    const days = this.configService.getOrThrow<number>(
      'retention.inventoryDays',
    );
    await ensureTtlIndex(
      this.inventoryModel.collection,
      'timestamp',
      days * 24 * 60 * 60,
    );
  }

  findLatestByDevice(deviceId: string): Promise<InventoryDocument | null> {
    return this.inventoryModel
      .findOne({ deviceId })
      .sort({ timestamp: -1 })
      .exec();
  }

  async insert(data: Inventory): Promise<InsertInventoryResult> {
    try {
      await this.inventoryModel.create(data);
      return { created: true };
    } catch (error) {
      if (isDuplicateKeyError(error)) {
        return { created: false };
      }
      throw error;
    }
  }
}
