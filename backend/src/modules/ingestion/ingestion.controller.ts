import {
  Body,
  Controller,
  HttpCode,
  HttpStatus,
  ParseArrayPipe,
  Post,
  UseGuards,
} from '@nestjs/common';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBody, ApiSecurity, ApiTags } from '@nestjs/swagger';
import { NodeApiKeyGuard } from '../devices/node-api-key.guard';
import { DeviceId } from '../devices/device-id.decorator';
import { DeviceCategoryParam } from '../devices/device-category.decorator';
import { DeviceCategory } from '../devices/device-category.enum';
import { IngestionService } from './ingestion.service';
import { InventoryIngestDto } from './dto/inventory-ingest.dto';
import { AccessEventIngestDto } from './dto/access-event-ingest.dto';
import { SacStatisticsService } from '../sac-statistics/sac-statistics.service';
import { SacStatisticIngestDto } from '../sac-statistics/dto/sac-statistic-ingest.dto';

// Not subject to the strict login brute-force profile (that's only for
// POST /auth/login) — still covered by the generous 'default' API-wide limit.
@ApiTags('ingestion')
@ApiSecurity('node-api-key')
@Controller()
@UseGuards(NodeApiKeyGuard)
@SkipThrottle({ login: true })
export class IngestionController {
  constructor(
    private readonly ingestionService: IngestionService,
    private readonly sacStatisticsService: SacStatisticsService,
  ) {}

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

  /**
   * BL-031: one database engine's daily statistics run — an ARRAY, because an
   * engine reports every database of its run in a single call.
   *
   * It lives here, with the other node-authenticated writes, rather than on
   * SacStatisticsController: that controller serves the JWT read half of the
   * same resource, and agent.md §5.4 forbids one endpoint (let alone one
   * controller) mixing the two auth mechanisms. Path and method are what make
   * an endpoint, so `POST /sac-statistics` here and `GET /sac-statistics` there
   * are two endpoints with one mechanism each.
   *
   * ParseArrayPipe is required for a top-level array body: the global
   * ValidationPipe validates the DTO of a request body, and an array is not one
   * — without this, `[{...garbage}]` would sail straight through. Its
   * validation options do NOT inherit from the global pipe, so
   * whitelist/forbidNonWhitelisted are restated here; that is what makes an
   * unknown field a 400 (CA-3) rather than a silent drop.
   */
  @Post('sac-statistics')
  @HttpCode(HttpStatus.CREATED)
  @ApiBody({ type: [SacStatisticIngestDto] })
  ingestSacStatistics(
    @DeviceId() deviceId: string,
    @Body(
      new ParseArrayPipe({
        items: SacStatisticIngestDto,
        whitelist: true,
        forbidNonWhitelisted: true,
      }),
    )
    dtos: SacStatisticIngestDto[],
  ) {
    return this.sacStatisticsService.ingest(deviceId, dtos);
  }
}
