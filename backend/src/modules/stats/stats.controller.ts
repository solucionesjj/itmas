import { Controller, Get, Query, UseGuards } from '@nestjs/common';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../auth/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { UserRole } from '../users/user-role.enum';
import { StatsService } from './stats.service';
import { SacAnalyticsService } from '../sac-statistics/sac-analytics.service';
import { QuerySacRankingDto } from '../sac-statistics/dto/query-sac-ranking.dto';
import { QuerySacGrowthDto } from '../sac-statistics/dto/query-sac-growth.dto';

// Open to all three roles, same rationale as DevicesController — declared
// explicitly per agent.md's "no undeclared-role endpoints" rule.
@ApiTags('stats')
@ApiBearerAuth('jwt')
@Controller('stats')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR, UserRole.USER, UserRole.AUDITOR)
@SkipThrottle({ login: true })
export class StatsController {
  constructor(
    private readonly statsService: StatsService,
    private readonly sacAnalyticsService: SacAnalyticsService,
  ) {}

  @Get('devices')
  getDeviceStats() {
    return this.statsService.getDeviceStats();
  }

  @Get('os')
  getOsStats() {
    return this.statsService.getOsStats();
  }

  // BL-033. Under /stats rather than on the sac-statistics resource because
  // these are aggregate analytics, not records — same distinction /stats/os
  // already draws against /devices.
  @Get('sac/ranking')
  getSacRanking(@Query() query: QuerySacRankingDto) {
    return this.sacAnalyticsService.ranking(query);
  }

  @Get('sac/growth')
  getSacGrowth(@Query() query: QuerySacGrowthDto) {
    return this.sacAnalyticsService.growth(query);
  }

  /** Feeds the analytics view's database selector. */
  @Get('sac/databases')
  getSacDatabases() {
    return this.sacAnalyticsService.listDatabaseNames();
  }
}
