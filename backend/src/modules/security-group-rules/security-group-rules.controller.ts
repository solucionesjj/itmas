import {
  Body,
  Controller,
  Get,
  Param,
  Patch,
  Query,
  Res,
  UseGuards,
} from '@nestjs/common';
import type { Response } from 'express';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../auth/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { UserRole } from '../users/user-role.enum';
import type { AuthenticatedUser } from '../auth/authenticated-user.interface';
import { AuditLogService } from '../audit-log/audit-log.service';
import { SecurityGroupRulesService } from './security-group-rules.service';
import { QuerySecurityGroupRulesDto } from './dto/query-security-group-rules.dto';
import { ExportSecurityGroupRulesDto } from './dto/export-security-group-rules.dto';
import { ReviewSecurityGroupRuleDto } from './dto/review-security-group-rule.dto';
import { AuthorizeSecurityGroupRuleDto } from './dto/authorize-security-group-rule.dto';

// Catalog reads (list/groups/export) are open to all three roles — Usuario
// gets confirmed read-only access to this audit catalog, same posture as
// /devices and the devices report (ADR-0013). Review/authorize are each
// locked to a single, different role below — that's the actual two-person
// separation-of-duties mechanism, not just RBAC boilerplate.
@ApiTags('security-group-rules')
@ApiBearerAuth('jwt')
@Controller('security-group-rules')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR, UserRole.AUDITOR, UserRole.USER)
@SkipThrottle({ login: true })
export class SecurityGroupRulesController {
  constructor(
    private readonly securityGroupRulesService: SecurityGroupRulesService,
    private readonly auditLogService: AuditLogService,
  ) {}

  @Get()
  findAll(@Query() query: QuerySecurityGroupRulesDto) {
    return this.securityGroupRulesService.findAll(query);
  }

  @Get('groups')
  listGroups() {
    return this.securityGroupRulesService.listGroups();
  }

  // The only non-JSON-envelope endpoint in this module — a raw file
  // download, same @Res() pattern as ReportsController.export().
  @Get('export')
  async export(
    @Query() query: ExportSecurityGroupRulesDto,
    @Res() res: Response,
  ): Promise<void> {
    const { buffer, contentType, filename } =
      await this.securityGroupRulesService.generateExport(query);

    res.set({
      'Content-Type': contentType,
      'Content-Disposition': `attachment; filename="${filename}"`,
    });
    res.send(buffer);
  }

  @Patch(':id/review')
  @Roles(UserRole.AUDITOR)
  async review(
    @Param('id') id: string,
    @CurrentUser() user: AuthenticatedUser,
    @Body() dto: ReviewSecurityGroupRuleDto,
  ) {
    const rule = await this.securityGroupRulesService.review(id, dto, user.sub);
    await this.auditLogService.record(
      'review_security_group_rule',
      user.sub,
      id,
      { observation: dto.observation },
    );
    return rule;
  }

  @Patch(':id/authorize')
  @Roles(UserRole.ADMINISTRATOR)
  async authorize(
    @Param('id') id: string,
    @CurrentUser() user: AuthenticatedUser,
    @Body() dto: AuthorizeSecurityGroupRuleDto,
  ) {
    const rule = await this.securityGroupRulesService.authorize(
      id,
      dto,
      user.sub,
    );
    await this.auditLogService.record(
      'authorize_security_group_rule',
      user.sub,
      id,
      { observation: dto.observation },
    );
    return rule;
  }
}
