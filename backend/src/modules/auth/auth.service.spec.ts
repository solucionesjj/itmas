import { ForbiddenException, UnauthorizedException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { JwtService } from '@nestjs/jwt';
import * as argon2 from 'argon2';
import { Types } from 'mongoose';
import { AuthService } from './auth.service';
import { UsersService } from '../users/users.service';
import { UsersRepository } from '../users/users.repository';
import { AuditLogService } from '../audit-log/audit-log.service';
import { UserRole } from '../users/user-role.enum';

describe('AuthService', () => {
  let authService: AuthService;
  let usersService: jest.Mocked<
    Pick<UsersService, 'findByUsername' | 'findById'>
  >;
  let usersRepository: jest.Mocked<
    Pick<
      UsersRepository,
      'touchLastLogin' | 'incrementTokenVersion' | 'setPasswordHash'
    >
  >;
  let auditLogService: jest.Mocked<Pick<AuditLogService, 'record'>>;
  let jwtService: JwtService;
  let configService: ConfigService;

  const config: Record<string, string | number> = {
    'jwt.accessSecret': 'access-secret',
    'jwt.refreshSecret': 'refresh-secret',
    'jwt.accessTtl': '15m',
    'jwt.refreshTtl': '7d',
  };

  const buildUser = async (overrides: Record<string, unknown> = {}) => ({
    _id: new Types.ObjectId(),
    username: 'jperez',
    email: 'jperez@empresa.com',
    passwordHash: await argon2.hash('Str0ngPass!'),
    role: UserRole.USER,
    active: true,
    mustChangePassword: false,
    tokenVersion: 0,
    ...overrides,
  });

  beforeEach(() => {
    usersService = { findByUsername: jest.fn(), findById: jest.fn() };
    usersRepository = {
      touchLastLogin: jest.fn(),
      incrementTokenVersion: jest.fn(),
      setPasswordHash: jest.fn(),
    };
    auditLogService = { record: jest.fn() };
    jwtService = new JwtService();
    configService = {
      getOrThrow: jest.fn((key: string) => {
        if (!(key in config)) throw new Error(`missing config ${key}`);
        return config[key];
      }),
    } as unknown as ConfigService;

    authService = new AuthService(
      usersService as unknown as UsersService,
      usersRepository as unknown as UsersRepository,
      jwtService,
      configService,
      auditLogService as unknown as AuditLogService,
    );
  });

  describe('login', () => {
    it('issues a token pair for valid credentials and records an audit entry', async () => {
      const user = await buildUser();
      usersService.findByUsername.mockResolvedValue(user as never);

      const result = await authService.login('jperez', 'Str0ngPass!');

      expect(result.accessToken).toBeDefined();
      expect(result.refreshToken).toBeDefined();
      expect(usersRepository.touchLastLogin).toHaveBeenCalledWith(user._id);
      expect(auditLogService.record).toHaveBeenCalledWith(
        'login',
        user._id,
        user._id.toString(),
      );

      const decoded = await jwtService.verifyAsync<Record<string, unknown>>(
        result.accessToken,
        { secret: 'access-secret' },
      );
      expect(decoded).toMatchObject({
        sub: user._id.toString(),
        username: 'jperez',
        role: UserRole.USER,
        mustChangePassword: false,
      });
    });

    it('rejects wrong password and records login_failed without revealing user existence', async () => {
      const user = await buildUser();
      usersService.findByUsername.mockResolvedValue(user as never);

      await expect(
        authService.login('jperez', 'wrong-password'),
      ).rejects.toThrow(UnauthorizedException);
      expect(auditLogService.record).toHaveBeenCalledWith(
        'login_failed',
        undefined,
        'jperez',
        { username: 'jperez' },
      );
    });

    it('rejects an unknown username the same way as a wrong password', async () => {
      usersService.findByUsername.mockResolvedValue(null);

      await expect(authService.login('ghost', 'whatever')).rejects.toThrow(
        UnauthorizedException,
      );
      expect(auditLogService.record).toHaveBeenCalledWith(
        'login_failed',
        undefined,
        'ghost',
        {
          username: 'ghost',
        },
      );
    });

    it('rejects an inactive user even with correct credentials', async () => {
      const user = await buildUser({ active: false });
      usersService.findByUsername.mockResolvedValue(user as never);

      await expect(authService.login('jperez', 'Str0ngPass!')).rejects.toThrow(
        UnauthorizedException,
      );
    });
  });

  describe('refresh', () => {
    it('rejects a refresh token whose tokenVersion no longer matches the DB', async () => {
      const user = await buildUser({ tokenVersion: 1 });
      usersService.findByUsername.mockResolvedValue(user as never);
      const { refreshToken } = await authService.login('jperez', 'Str0ngPass!');

      // Simulate the user having logged out since, bumping tokenVersion.
      usersService.findById.mockResolvedValue({
        ...user,
        tokenVersion: 2,
      } as never);

      await expect(authService.refresh(refreshToken)).rejects.toThrow(
        UnauthorizedException,
      );
    });

    it('issues a new token pair when tokenVersion matches', async () => {
      const user = await buildUser({ tokenVersion: 0 });
      usersService.findByUsername.mockResolvedValue(user as never);
      const { refreshToken } = await authService.login('jperez', 'Str0ngPass!');

      usersService.findById.mockResolvedValue(user as never);

      const result = await authService.refresh(refreshToken);
      expect(result.accessToken).toBeDefined();
      expect(result.refreshToken).toBeDefined();
    });

    it('rejects a garbage refresh token', async () => {
      await expect(authService.refresh('not-a-jwt')).rejects.toThrow(
        UnauthorizedException,
      );
    });
  });

  describe('logout', () => {
    it('bumps the tokenVersion and records an audit entry', async () => {
      await authService.logout('user-id-123');

      expect(usersRepository.incrementTokenVersion).toHaveBeenCalledWith(
        'user-id-123',
      );
      expect(auditLogService.record).toHaveBeenCalledWith(
        'logout',
        'user-id-123',
        'user-id-123',
      );
    });
  });

  describe('changePassword', () => {
    it('rejects when the current password is incorrect', async () => {
      const user = await buildUser();
      usersService.findById.mockResolvedValue(user as never);

      await expect(
        authService.changePassword(user._id.toString(), 'wrong', 'NewPassw0rd'),
      ).rejects.toThrow(ForbiddenException);
      expect(usersRepository.setPasswordHash).not.toHaveBeenCalled();
    });

    it('updates the password hash and records an audit entry on success', async () => {
      const user = await buildUser();
      usersService.findById.mockResolvedValue(user as never);

      await authService.changePassword(
        user._id.toString(),
        'Str0ngPass!',
        'NewPassw0rd1',
      );

      expect(usersRepository.setPasswordHash).toHaveBeenCalledWith(
        user._id,
        expect.any(String),
      );
      expect(auditLogService.record).toHaveBeenCalledWith(
        'change_password',
        user._id.toString(),
        user._id.toString(),
      );
    });
  });
});
