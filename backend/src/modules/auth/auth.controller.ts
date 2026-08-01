import {
  Body,
  Controller,
  HttpCode,
  HttpStatus,
  Post,
  UseGuards,
} from '@nestjs/common';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { AuthService } from './auth.service';
import { LoginDto } from './dto/login.dto';
import { RefreshDto } from './dto/refresh.dto';
import { ChangePasswordDto } from './dto/change-password.dto';
import { JwtAuthGuard } from './jwt-auth.guard';
import { CurrentUser } from '../../common/decorators/current-user.decorator';
import type { AuthenticatedUser } from './authenticated-user.interface';

// Global ThrottlerGuard (APP_GUARD, see app.module.ts) checks every route
// against BOTH named profiles ('default' generous API-wide limit, 'login'
// strict brute-force limit) unless skipped. Only `login` below is meant to be
// bounded by the strict 'login' profile — every other route on this
// controller opts out of it via @SkipThrottle({ login: true }) and falls back
// to the generous 'default' profile like the rest of the API.
@ApiTags('auth')
@Controller('auth')
export class AuthController {
  constructor(private readonly authService: AuthService) {}

  @Post('login')
  @HttpCode(HttpStatus.OK)
  login(@Body() dto: LoginDto) {
    return this.authService.login(dto.username, dto.password);
  }

  @Post('refresh')
  @HttpCode(HttpStatus.OK)
  @SkipThrottle({ login: true })
  refresh(@Body() dto: RefreshDto) {
    return this.authService.refresh(dto.refreshToken);
  }

  @Post('logout')
  @HttpCode(HttpStatus.NO_CONTENT)
  @SkipThrottle({ login: true })
  @UseGuards(JwtAuthGuard)
  @ApiBearerAuth('jwt')
  async logout(@CurrentUser() user: AuthenticatedUser): Promise<void> {
    await this.authService.logout(user.sub);
  }

  @Post('change-password')
  @HttpCode(HttpStatus.NO_CONTENT)
  @SkipThrottle({ login: true })
  @UseGuards(JwtAuthGuard)
  @ApiBearerAuth('jwt')
  async changePassword(
    @CurrentUser() user: AuthenticatedUser,
    @Body() dto: ChangePasswordDto,
  ): Promise<void> {
    await this.authService.changePassword(
      user.sub,
      dto.currentPassword,
      dto.newPassword,
    );
  }
}
