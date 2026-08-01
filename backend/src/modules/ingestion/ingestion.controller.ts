import {
  Body,
  Controller,
  HttpCode,
  HttpStatus,
  Post,
  UseGuards,
} from '@nestjs/common';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiSecurity, ApiTags } from '@nestjs/swagger';
import { NodeApiKeyGuard } from '../devices/node-api-key.guard';
import { DeviceId } from '../devices/device-id.decorator';
import { DeviceCategoryParam } from '../devices/device-category.decorator';
import { DeviceCategory } from '../devices/device-category.enum';
import { IngestionService } from './ingestion.service';
import { InventoryIngestDto } from './dto/inventory-ingest.dto';
import { AccessEventIngestDto } from './dto/access-event-ingest.dto';

// Not subject to the strict login brute-force profile (that's only for
// POST /auth/login) — still covered by the generous 'default' API-wide limit.
@ApiTags('ingestion')
@ApiSecurity('node-api-key')
@Controller()
@UseGuards(NodeApiKeyGuard)
@SkipThrottle({ login: true })
export class IngestionController {
  constructor(private readonly ingestionService: IngestionService) {}

  @Post('inventory')
  @HttpCode(HttpStatus.CREATED)
  ingestInventory(
    @DeviceId() deviceId: string,
    @Body() dto: InventoryIngestDto,
  ) {
    return this.ingestionService.ingestInventory(deviceId, dto);
  }

  @Post('access-events')
  @HttpCode(HttpStatus.CREATED)
  ingestAccessEvent(
    @DeviceId() deviceId: string,
    @DeviceCategoryParam() deviceCategory: DeviceCategory,
    @Body() dto: AccessEventIngestDto,
  ) {
    return this.ingestionService.ingestAccessEvent(
      deviceId,
      deviceCategory,
      dto,
    );
  }
}
