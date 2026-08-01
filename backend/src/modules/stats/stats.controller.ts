import { Controller, Get, UseGuards } from '@nestjs/common';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../auth/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { UserRole } from '../users/user-role.enum';
import { StatsService } from './stats.service';

// Open to all three roles, same rationale as DevicesController — declared
// explicitly per agent.md's "no undeclared-role endpoints" rule.
@ApiTags('stats')
@ApiBearerAuth('jwt')
@Controller('stats')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR, UserRole.USER, UserRole.AUDITOR)
@SkipThrottle({ login: true })
export class StatsController {
  constructor(private readonly statsService: StatsService) {}

  @Get('devices')
  getDeviceStats() {
    return this.statsService.getDeviceStats();
  }

  @Get('os')
  getOsStats() {
    return this.statsService.getOsStats();
  }
}
