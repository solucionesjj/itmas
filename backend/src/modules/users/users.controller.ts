import {
  Body,
  Controller,
  Get,
  HttpCode,
  HttpStatus,
  Param,
  Patch,
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
import { UserRole } from './user-role.enum';
import { UsersService } from './users.service';
import { CreateUserDto } from './dto/create-user.dto';
import { UpdateUserDto } from './dto/update-user.dto';
import { QueryUsersDto } from './dto/query-users.dto';
import { AuditLogService } from '../audit-log/audit-log.service';
import type { AuthenticatedUser } from '../auth/authenticated-user.interface';

@ApiTags('users')
@ApiBearerAuth('jwt')
@Controller('users')
@UseGuards(JwtAuthGuard, RolesGuard)
@Roles(UserRole.ADMINISTRATOR)
@SkipThrottle({ login: true })
export class UsersController {
  constructor(
    private readonly usersService: UsersService,
    private readonly auditLogService: AuditLogService,
  ) {}

  @Get()
  findAll(@Query() query: QueryUsersDto) {
    return this.usersService.findAllForAdmin(query);
  }

  @Post()
  @HttpCode(HttpStatus.CREATED)
  async create(
    @CurrentUser() admin: AuthenticatedUser,
    @Body() dto: CreateUserDto,
  ) {
    const user = await this.usersService.createByAdmin(dto, admin.sub);
    await this.auditLogService.record('create_user', admin.sub, user._id, {
      username: dto.username,
      role: dto.role,
    });
    return user;
  }

  @Patch(':id')
  async update(
    @Param('id') id: string,
    @CurrentUser() admin: AuthenticatedUser,
    @Body() dto: UpdateUserDto,
  ) {
    const { user, changedFields } = await this.usersService.updateByAdmin(
      id,
      dto,
      admin.sub,
    );
    await this.auditLogService.record(
      'update_user',
      admin.sub,
      id,
      changedFields,
    );
    return user;
  }
}
