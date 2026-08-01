import { Injectable } from '@nestjs/common';
import { DevicesService } from '../devices/devices.service';
import { DeviceCategory } from '../devices/device-category.enum';
import { InventoriesRepository } from '../inventories/inventories.repository';
import { InventoryDiffService } from '../inventories/inventory-diff.service';
import { AccessEventsRepository } from '../access-events/access-events.repository';
import { AlertEngineService } from '../alert-engine/alert-engine.service';
import { JsonLoggerService } from '../../common/logger/json-logger.service';
import { InventoryIngestDto } from './dto/inventory-ingest.dto';
import { AccessEventIngestDto } from './dto/access-event-ingest.dto';

export interface IngestAck {
  deviceId: string;
  timestamp: string;
}

@Injectable()
export class IngestionService {
  constructor(
    private readonly devicesService: DevicesService,
    private readonly inventoriesRepository: InventoriesRepository,
    private readonly inventoryDiffService: InventoryDiffService,
    private readonly accessEventsRepository: AccessEventsRepository,
    private readonly alertEngineService: AlertEngineService,
    private readonly logger: JsonLoggerService,
  ) {
    this.logger.setContext(IngestionService.name);
  }

  async ingestInventory(
    deviceId: string,
    dto: InventoryIngestDto,
  ): Promise<IngestAck> {
    // Fetched before insert: after insert this would just return the record
    // we're about to (or already did) write.
    const previous =
      await this.inventoriesRepository.findLatestByDevice(deviceId);

    const { created } = await this.inventoriesRepository.insert({
      deviceId,
      timestamp: new Date(dto.timestamp),
      cpu: dto.cpu,
      ram: dto.ram,
      disks: dto.disks,
    });

    await this.devicesService.touchOnIngest(deviceId, {
      hostname: dto.hostname,
      category: dto.category,
      os: dto.os,
    });

    // Only diff/log on a genuine new record — an idempotent retry of an
    // already-seen inventory isn't a new change to report.
    if (created) {
      const diff = this.inventoryDiffService.compare(previous, {
        cpu: dto.cpu,
        ram: dto.ram,
        disks: dto.disks,
      });

      if (diff.changed) {
        this.logger.log('Resource change detected', {
          deviceId,
          changes: diff.changes,
        });
        await this.alertEngineService.evaluateResourceChange(
          deviceId,
          diff.changes,
        );
      }
    }

    return { deviceId, timestamp: dto.timestamp };
  }

  async ingestAccessEvent(
    deviceId: string,
    deviceCategory: DeviceCategory,
    dto: AccessEventIngestDto,
  ): Promise<IngestAck> {
    const { created } = await this.accessEventsRepository.insert({
      deviceId,
      level: dto.level,
      user: dto.user,
      timestamp: new Date(dto.timestamp),
      action: dto.action,
    });

    await this.devicesService.touchLastSeen(deviceId);

    // Only evaluate on a genuinely new record — an idempotent retry of an
    // already-seen event isn't a new access to alert on.
    if (created) {
      await this.alertEngineService.evaluateAccessEvent({
        deviceId,
        category: deviceCategory,
        level: dto.level,
        user: dto.user,
        action: dto.action,
        timestamp: new Date(dto.timestamp),
      });
    }

    return { deviceId, timestamp: dto.timestamp };
  }
}
