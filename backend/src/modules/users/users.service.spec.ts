import { ConflictException, ForbiddenException } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { Types } from 'mongoose';
import { UsersService } from './users.service';
import { UsersRepository } from './users.repository';
import { UserRole } from './user-role.enum';
import { JsonLoggerService } from '../../common/logger/json-logger.service';
import { isDuplicateKeyError } from '../../common/mongo/duplicate-key.util';

jest.mock('argon2', () => ({
  hash: jest.fn((value: string) => Promise.resolve(`hashed:${value}`)),
}));

describe('UsersService (admin management)', () => {
  let usersService: UsersService;
  let usersRepository: jest.Mocked<
    Pick<
      UsersRepository,
      'findById' | 'findAll' | 'createUser' | 'applyAdminUpdate'
    >
  >;
  const logger = { setContext: jest.fn() } as unknown as JsonLoggerService;
  const configService = {} as ConfigService;

  const buildUser = (overrides: Record<string, unknown> = {}) => ({
    _id: new Types.ObjectId(),
    username: 'jperez',
    email: 'jperez@empresa.com',
    role: UserRole.USER,
    active: true,
    mustChangePassword: false,
    tokenVersion: 0,
    createdAt: new Date(),
    ...overrides,
  });

  beforeEach(() => {
    usersRepository = {
      findById: jest.fn(),
      findAll: jest.fn(),
      createUser: jest.fn(),
      applyAdminUpdate: jest.fn(),
    };
    usersService = new UsersService(
      usersRepository as unknown as UsersRepository,
      configService,
      logger,
    );
  });

  describe('findAllForAdmin', () => {
    it('passes only the defined filter fields through to the repository', async () => {
      usersRepository.findAll.mockResolvedValue([buildUser() as never]);

      const result = await usersService.findAllForAdmin({
        role: UserRole.AUDITOR,
        active: undefined,
      });

      expect(usersRepository.findAll).toHaveBeenCalledWith({
        role: UserRole.AUDITOR,
      });
      expect(result).toHaveLength(1);
      expect(result[0]).not.toHaveProperty('passwordHash');
    });
  });

  describe('createByAdmin', () => {
    it('creates a user and never returns passwordHash/tokenVersion', async () => {
      const created = buildUser();
      usersRepository.createUser.mockResolvedValue({
        created: true,
        user: created as never,
      });

      const result = await usersService.createByAdmin(
        {
          username: 'jperez',
          email: 'jperez@empresa.com',
          password: 'Str0ngPass!',
          role: UserRole.USER,
        },
        new Types.ObjectId().toString(),
      );

      expect(result).not.toHaveProperty('passwordHash');
      expect(result).not.toHaveProperty('tokenVersion');
      expect(usersRepository.createUser).toHaveBeenCalledWith(
        expect.objectContaining({
          username: 'jperez',
          mustChangePassword: true,
          tokenVersion: 0,
        }),
      );
    });

    it('throws ConflictException on a duplicate username/email', async () => {
      usersRepository.createUser.mockResolvedValue({ created: false });

      await expect(
        usersService.createByAdmin(
          {
            username: 'jperez',
            email: 'jperez@empresa.com',
            password: 'Str0ngPass!',
            role: UserRole.USER,
          },
          new Types.ObjectId().toString(),
        ),
      ).rejects.toThrow(ConflictException);
    });
  });

  describe('updateByAdmin', () => {
    it('rejects an admin deactivating their own account', async () => {
      const admin = buildUser({ role: UserRole.ADMINISTRATOR });
      const adminId = admin._id.toString();

      await expect(
        usersService.updateByAdmin(adminId, { active: false }, adminId),
      ).rejects.toThrow(ForbiddenException);
      expect(usersRepository.findById).not.toHaveBeenCalled();
    });

    it('rejects an admin demoting their own account', async () => {
      const admin = buildUser({ role: UserRole.ADMINISTRATOR });
      const adminId = admin._id.toString();

      await expect(
        usersService.updateByAdmin(adminId, { role: UserRole.USER }, adminId),
      ).rejects.toThrow(ForbiddenException);
    });

    it('allows an admin to edit their own email', async () => {
      const admin = buildUser({ role: UserRole.ADMINISTRATOR });
      const adminId = admin._id.toString();
      usersRepository.findById.mockResolvedValue(admin as never);
      usersRepository.applyAdminUpdate.mockResolvedValue({
        ...admin,
        email: 'new@empresa.com',
      } as never);

      const result = await usersService.updateByAdmin(
        adminId,
        { email: 'new@empresa.com' },
        adminId,
      );
      expect(result.user.email).toBe('new@empresa.com');
    });

    it('hashes a password reset, forcing mustChangePassword via the repository call', async () => {
      const user = buildUser();
      usersRepository.findById.mockResolvedValue(user as never);
      usersRepository.applyAdminUpdate.mockResolvedValue({
        ...user,
        mustChangePassword: true,
      } as never);

      const result = await usersService.updateByAdmin(
        user._id.toString(),
        { password: 'BrandNewPass1' },
        'some-other-admin-id',
      );

      expect(usersRepository.applyAdminUpdate).toHaveBeenCalledWith(
        user._id.toString(),
        expect.objectContaining({ passwordHash: 'hashed:BrandNewPass1' }),
      );
      expect(result.changedFields).toEqual({ passwordReset: true });
      expect(result.changedFields).not.toHaveProperty('password');
    });

    it('maps a duplicate-key error from an email change to ConflictException', async () => {
      const user = buildUser();
      usersRepository.findById.mockResolvedValue(user as never);
      const duplicateError = Object.assign(new Error('E11000'), {
        code: 11000,
      });
      expect(isDuplicateKeyError(duplicateError)).toBe(true);
      usersRepository.applyAdminUpdate.mockRejectedValue(duplicateError);

      await expect(
        usersService.updateByAdmin(
          user._id.toString(),
          { email: 'taken@empresa.com' },
          'some-other-admin-id',
        ),
      ).rejects.toThrow(ConflictException);
    });
  });
});
