import {
  Controller,
  Get,
  HttpCode,
  HttpStatus,
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
import type { AuthenticatedUser } from '../auth/authenticated-user.interface';
import { SecurityGroupSyncService } from './security-group-sync.service';
import { AwsSyncTriggerType } from './aws-sync-trigger-type.enum';
import { QueryAwsSyncRunsDto } from './dto/query-aws-sync-runs.dto';

// Administrador + Auditor only — Usuario has no operational need to trigger
// or inspect sync runs, only to see their result in the catalog itself
// (ADR-0013), unlike security-group-rules' read endpoints which do admit
// Usuario.
@ApiTags('security-group-sync')
@ApiBearerAuth('jwt')
@Controller('security-group-sync')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR, UserRole.AUDITOR)
@SkipThrottle({ login: true })
export class SecurityGroupSyncController {
  constructor(private readonly syncService: SecurityGroupSyncService) {}

  @Post('run')
  @HttpCode(HttpStatus.CREATED)
  run(@CurrentUser() user: AuthenticatedUser) {
    return this.syncService.runSync(AwsSyncTriggerType.MANUAL, user.sub);
  }

  @Get('runs')
  listRuns(@Query() query: QueryAwsSyncRunsDto) {
    return this.syncService.listRuns(query.limit ?? 20);
  }

  @Get('summary')
  getSummary() {
    return this.syncService.getSummary();
  }
}
