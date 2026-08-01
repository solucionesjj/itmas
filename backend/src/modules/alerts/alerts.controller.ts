import {
  Body,
  Controller,
  Get,
  Param,
  Patch,
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
import { AlertsService } from './alerts.service';
import { QueryAlertsDto } from './dto/query-alerts.dto';
import { UpdateAlertDto } from './dto/update-alert.dto';
import { AuditLogService } from '../audit-log/audit-log.service';
import type { AuthenticatedUser } from '../auth/authenticated-user.interface';

@ApiTags('alerts')
@ApiBearerAuth('jwt')
@Controller('alerts')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR, UserRole.AUDITOR)
@SkipThrottle({ login: true })
export class AlertsController {
  constructor(
    private readonly alertsService: AlertsService,
    private readonly auditLogService: AuditLogService,
  ) {}

  @Get()
  findAll(@Query() query: QueryAlertsDto) {
    return this.alertsService.findAll(query);
  }

  @Patch(':id')
  async updateStatus(
    @Param('id') id: string,
    @CurrentUser() user: AuthenticatedUser,
    @Body() dto: UpdateAlertDto,
  ) {
    const { alert, previousStatus } = await this.alertsService.updateStatus(
      id,
      dto.status,
    );
    await this.auditLogService.record('update_alert_status', user.sub, id, {
      from: previousStatus,
      to: alert.status,
    });
    return alert;
  }
}
