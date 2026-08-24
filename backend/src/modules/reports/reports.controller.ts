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
    const file = await this.reportsService.generate(user, query);

    res.set({
      'Content-Type': file.contentType,
      'Content-Disposition': `attachment; filename="${file.filename}"`,
    });

    if (file.kind === 'buffer') {
      res.send(file.buffer);
      return;
    }
    // The streaming reports own the response body and end it themselves
    // (BL-032 CA-6) — no res.send()/res.end() here, which would either
    // truncate the stream or double-end it. Note the headers above are already
    // flushed by the time the first row is written, so a failure mid-stream
    // truncates the file rather than becoming a JSON error; that is inherent to
    // streaming an unbounded result set.
    await file.write(res);
  }
}
