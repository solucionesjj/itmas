import { Controller, Get, Query, UseGuards } from '@nestjs/common';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../auth/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { UserRole } from '../users/user-role.enum';
import { DevicesService } from './devices.service';
import { QueryDevicesDto } from './dto/query-devices.dto';

// Query/read-only surface for the portal — separate concern from the
// node-ingestion side of this module (NodeApiKeyGuard, untouched here).
// Open to all three roles (Administrador/Usuario/Auditor all consult
// dashboards/reports); still declared explicitly per agent.md's rule that
// an endpoint with no declared role requirement is an implementation error.
@ApiTags('devices')
@ApiBearerAuth('jwt')
@Controller('devices')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR, UserRole.USER, UserRole.AUDITOR)
@SkipThrottle({ login: true })
export class DevicesController {
  constructor(private readonly devicesService: DevicesService) {}

  @Get()
  findAll(@Query() query: QueryDevicesDto) {
    return this.devicesService.findAllForPortal(query);
  }
}
