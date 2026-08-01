import {
  Body,
  Controller,
  Get,
  HttpCode,
  HttpStatus,
  Param,
  Patch,
  Post,
  UseGuards,
} from '@nestjs/common';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../auth/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { UserRole } from '../users/user-role.enum';
import { AlertRulesService } from './alert-rules.service';
import { CreateAlertRuleDto } from './dto/create-alert-rule.dto';
import { UpdateAlertRuleDto } from './dto/update-alert-rule.dto';
import { AuditLogService } from '../audit-log/audit-log.service';
import type { AuthenticatedUser } from '../auth/authenticated-user.interface';

@ApiTags('alert-rules')
@ApiBearerAuth('jwt')
@Controller('alert-rules')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR)
@SkipThrottle({ login: true })
export class AlertRulesController {
  constructor(
    private readonly alertRulesService: AlertRulesService,
    private readonly auditLogService: AuditLogService,
  ) {}

  @Get()
  findAll() {
    return this.alertRulesService.findAll();
  }

  @Post()
  @HttpCode(HttpStatus.CREATED)
  async create(
    @CurrentUser() user: AuthenticatedUser,
    @Body() dto: CreateAlertRuleDto,
  ) {
    const rule = await this.alertRulesService.create(dto, user.sub);
    await this.auditLogService.record(
      'update_alert_rule',
      user.sub,
      (rule._id as { toString(): string }).toString(),
      { action: 'create', type: rule.type },
    );
    return rule;
  }

  @Patch(':id')
  async update(
    @Param('id') id: string,
    @CurrentUser() user: AuthenticatedUser,
    @Body() dto: UpdateAlertRuleDto,
  ) {
    const rule = await this.alertRulesService.update(id, dto);
    await this.auditLogService.record('update_alert_rule', user.sub, id, {
      action: 'update',
      ...dto,
    });
    return rule;
  }
}
