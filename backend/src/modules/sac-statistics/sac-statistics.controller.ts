import { Controller, Get, Query, UseGuards } from '@nestjs/common';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../auth/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { UserRole } from '../users/user-role.enum';
import { SacStatisticsService } from './sac-statistics.service';
import { QuerySacStatisticsDto } from './dto/query-sac-statistics.dto';

/**
 * The JWT half of the `sac-statistics` resource (BL-032). Its node-authenticated
 * write half lives in IngestionController — deliberately a different controller
 * with a different guard, because agent.md §5.4 forbids mixing the two auth
 * mechanisms on one endpoint. Sharing the path while splitting the controller is
 * how that boundary stays visible in the code.
 *
 * All three roles, declared explicitly: these are general consultation data, but
 * an endpoint with no declared role requirement is treated as a bug regardless.
 */
@ApiTags('sac-statistics')
@ApiBearerAuth('jwt')
@Controller('sac-statistics')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR, UserRole.USER, UserRole.AUDITOR)
@SkipThrottle({ login: true })
export class SacStatisticsController {
  constructor(private readonly sacStatisticsService: SacStatisticsService) {}

  @Get()
  findAll(@Query() query: QuerySacStatisticsDto) {
    return this.sacStatisticsService.findAll(query);
  }
}
