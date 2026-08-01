import {
  ForbiddenException,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService, JwtSignOptions } from '@nestjs/jwt';
import * as argon2 from 'argon2';
import { UsersService } from '../users/users.service';
import { UsersRepository } from '../users/users.repository';
import { AuditLogService } from '../audit-log/audit-log.service';
import type { UserDocument } from '../users/user.schema';
import type {
  AuthenticatedUser,
  RefreshTokenPayload,
} from './authenticated-user.interface';

export interface TokenPair {
  accessToken: string;
  refreshToken: string;
}

type ExpiresIn = JwtSignOptions['expiresIn'];

@Injectable()
export class AuthService {
  constructor(
    private readonly usersService: UsersService,
    private readonly usersRepository: UsersRepository,
    private readonly jwtService: JwtService,
    private readonly configService: ConfigService,
    private readonly auditLogService: AuditLogService,
  ) {}

  async login(username: string, password: string): Promise<TokenPair> {
    const user = await this.usersService.findByUsername(username);

    // Constant-shape failure path: verify against a dummy hash when the user
    // doesn't exist, so login timing doesn't reveal whether a username exists.
    const passwordHash =
      user?.passwordHash ?? (await argon2.hash('placeholder-invalid'));
    const passwordValid = await argon2
      .verify(passwordHash, password)
      .catch(() => false);

    if (!user || !user.active || !passwordValid) {
      await this.auditLogService.record('login_failed', undefined, username, {
        username,
      });
      throw new UnauthorizedException('Invalid credentials');
    }

    await this.usersRepository.touchLastLogin(user._id);
    await this.auditLogService.record('login', user._id, user._id.toString());

    return this.issueTokenPair(user);
  }

  async refresh(refreshToken: string): Promise<TokenPair> {
    const payload = await this.verifyRefreshToken(refreshToken);
    const user = await this.usersService.findById(payload.sub);

    if (!user || !user.active || user.tokenVersion !== payload.tokenVersion) {
      throw new UnauthorizedException('Refresh token is no longer valid');
    }

    return this.issueTokenPair(user);
  }

  async logout(userId: string): Promise<void> {
    await this.usersRepository.incrementTokenVersion(userId);
    await this.auditLogService.record('logout', userId, userId);
  }

  async changePassword(
    userId: string,
    currentPassword: string,
    newPassword: string,
  ): Promise<void> {
    const user = await this.usersService.findById(userId);
    if (!user) {
      throw new UnauthorizedException('User not found');
    }

    const currentValid = await argon2.verify(
      user.passwordHash,
      currentPassword,
    );
    if (!currentValid) {
      throw new ForbiddenException('Current password is incorrect');
    }

    const newHash = await argon2.hash(newPassword);
    await this.usersRepository.setPasswordHash(user._id, newHash);
    await this.auditLogService.record('change_password', userId, userId);
  }

  private async issueTokenPair(user: UserDocument): Promise<TokenPair> {
    const accessPayload: AuthenticatedUser = {
      sub: user._id.toString(),
      username: user.username,
      role: user.role,
      mustChangePassword: user.mustChangePassword,
    };
    const refreshPayload: RefreshTokenPayload = {
      sub: user._id.toString(),
      tokenVersion: user.tokenVersion,
    };

    const accessToken = await this.jwtService.signAsync(accessPayload, {
      secret: this.configService.getOrThrow<string>('jwt.accessSecret'),
      expiresIn: this.configService.getOrThrow<string>(
        'jwt.accessTtl',
      ) as unknown as ExpiresIn,
    });
    const refreshToken = await this.jwtService.signAsync(refreshPayload, {
      secret: this.configService.getOrThrow<string>('jwt.refreshSecret'),
      expiresIn: this.configService.getOrThrow<string>(
        'jwt.refreshTtl',
      ) as unknown as ExpiresIn,
    });

    return { accessToken, refreshToken };
  }

  private async verifyRefreshToken(
    token: string,
  ): Promise<RefreshTokenPayload> {
    try {
      return await this.jwtService.verifyAsync<RefreshTokenPayload>(token, {
        secret: this.configService.getOrThrow<string>('jwt.refreshSecret'),
      });
    } catch {
      throw new UnauthorizedException('Invalid or expired refresh token');
    }
  }
}
