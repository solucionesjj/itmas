import {
  Body,
  Controller,
  Get,
  HttpCode,
  HttpStatus,
  Param,
  Post,
  Query,
  UseGuards,
} from '@nestjs/common';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../auth/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { UserRole } from '../users/user-role.enum';
import { DevicesService } from './devices.service';
import { QueryDevicesDto } from './dto/query-devices.dto';
import { CreateDeviceDto } from './dto/create-device.dto';
import {
  CreateDeviceResponse,
  RotateDeviceKeyResponse,
} from './device-response.mapper';
import { AuditLogService } from '../audit-log/audit-log.service';
import type { AuthenticatedUser } from '../auth/authenticated-user.interface';

// Query/read-only surface for the portal — separate concern from the
// node-ingestion side of this module (NodeApiKeyGuard, untouched here).
// Open to all three roles (Administrador/Usuario/Auditor all consult
// dashboards/reports); still declared explicitly per agent.md's rule that
// an endpoint with no declared role requirement is an implementation error.
//
// POST / and POST /:id/rotate-key (ADR-0016) are the exception to that
// "all three roles" default — each carries its own method-level @Roles()
// restricting to Administrador only, which RolesGuard's
// getAllAndOverride() resolves in preference to the class-level one above.
@ApiTags('devices')
@ApiBearerAuth('jwt')
@Controller('devices')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR, UserRole.USER, UserRole.AUDITOR)
@SkipThrottle({ login: true })
export class DevicesController {
  constructor(
    private readonly devicesService: DevicesService,
    private readonly auditLogService: AuditLogService,
  ) {}

  @Get()
  findAll(@Query() query: QueryDevicesDto) {
    return this.devicesService.findAllForPortal(query);
  }

  /**
   * Administrador-only (ADR-0016, reversing ADR-0003's CLI-only decision).
   * Calls the exact same `DevicesService.provision()` the CLI script uses —
   * no duplicated key-generation/hashing logic. `apiKey` plaintext appears
   * in this response body and nowhere else; it is never persisted and never
   * retrievable again after this call returns.
   */
  @Post()
  @Roles(UserRole.ADMINISTRATOR)
  @HttpCode(HttpStatus.CREATED)
  async create(
    @CurrentUser() admin: AuthenticatedUser,
    @Body() dto: CreateDeviceDto,
  ): Promise<CreateDeviceResponse> {
    const { deviceId, apiKey } = await this.devicesService.provision({
      hostname: dto.hostname,
      category: dto.category,
    });
    // Audit detail intentionally omits apiKey/secret — never log plaintext
    // key material, only the non-sensitive identifying fields.
    await this.auditLogService.record('create_device', admin.sub, deviceId, {
      hostname: dto.hostname,
      category: dto.category,
    });
    return { deviceId, hostname: dto.hostname, category: dto.category, apiKey };
  }

  /**
   * Administrador-only (ADR-0016). Calls the exact same
   * `DevicesService.rotateKey()` the CLI script uses. `404` if the device
   * doesn't exist (service throws `NotFoundException`). `200`, not `201` —
   * this mutates an existing resource, it doesn't create one.
   */
  @Post(':id/rotate-key')
  @Roles(UserRole.ADMINISTRATOR)
  @HttpCode(HttpStatus.OK)
  async rotateKey(
    @Param('id') id: string,
    @CurrentUser() admin: AuthenticatedUser,
  ): Promise<RotateDeviceKeyResponse> {
    const { deviceId, apiKey } = await this.devicesService.rotateKey(id);
    await this.auditLogService.record('rotate_device_key', admin.sub, id);
    return { deviceId, apiKey };
  }
}
