import { Controller, Get, Query, Res, UseGuards } from '@nestjs/common';
import type { Response } from 'express';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtAuthGuard } from '../auth/jwt-auth.guard';
import { RolesGuard } from '../../common/guards/roles.guard';
import { Roles } from '../../common/decorators/roles.decorator';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import { UserRole } from '../users/user-role.enum';
import type { AuthenticatedUser } from '../auth/authenticated-user.interface';
import { ReportsService } from './reports.service';
import { QueryReportsDto } from './dto/query-reports.dto';

// Open to all three roles at the guard level (Usuario may export the devices
// report per RF-14/CA-13) — the alerts-report path is further restricted to
// Administrador/Auditor inside ReportsService, since a single endpoint here
// serves two different authorization scopes.
@ApiTags('reports')
@ApiBearerAuth('jwt')
@Controller('reports')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR, UserRole.USER, UserRole.AUDITOR)
@SkipThrottle({ login: true })
export class ReportsController {
  constructor(private readonly reportsService: ReportsService) {}

  // The only endpoint in the API that isn't the standard JSON envelope — a
  // raw file download. @Res() (no passthrough) hands full control of the
  // response to this handler; AllExceptionsFilter still catches anything
  // thrown before this point and replies with the usual JSON error shape.
  @Get('export')
  async export(
    @CurrentUser() user: AuthenticatedUser,
    @Query() query: QueryReportsDto,
    @Res() res: Response,
  ): Promise<void> {
    const { buffer, contentType, filename } =
      await this.reportsService.generate(user, query);

    res.set({
      'Content-Type': contentType,
      'Content-Disposition': `attachment; filename="${filename}"`,
    });
    res.send(buffer);
  }
}
