import {
  ConflictException,
  ForbiddenException,
  Injectable,
  NotFoundException,
  OnModuleInit,
} from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import * as argon2 from 'argon2';
import { Types } from 'mongoose';
import { UsersListFilter, UsersRepository } from './users.repository';
import { UserDocument } from './user.schema';
import { UserRole } from './user-role.enum';
import { CreateUserDto } from './dto/create-user.dto';
import { UpdateUserDto } from './dto/update-user.dto';
import { QueryUsersDto } from './dto/query-users.dto';
import { toUserResponse, UserResponse } from './user-response.mapper';
import { isDuplicateKeyError } from '../../common/mongo/duplicate-key.util';
import { JsonLoggerService } from '../../common/logger/json-logger.service';

export interface UpdateByAdminResult {
  user: UserResponse;
  changedFields: Record<string, unknown>;
}

@Injectable()
export class UsersService implements OnModuleInit {
  constructor(
    private readonly usersRepository: UsersRepository,
    private readonly configService: ConfigService,
    private readonly logger: JsonLoggerService,
  ) {
    this.logger.setContext(UsersService.name);
  }

  findByUsername(username: string): Promise<UserDocument | null> {
    return this.usersRepository.findByUsername(username);
  }

  findById(id: string): Promise<UserDocument | null> {
    return this.usersRepository.findById(id);
  }

  async findAllForAdmin(query: QueryUsersDto): Promise<UserResponse[]> {
    const filter: UsersListFilter = {};
    if (query.role !== undefined) {
      filter.role = query.role;
    }
    if (query.active !== undefined) {
      filter.active = query.active;
    }
    const users = await this.usersRepository.findAll(filter);
    return users.map(toUserResponse);
  }

  async createByAdmin(
    dto: CreateUserDto,
    createdBy: string,
  ): Promise<UserResponse> {
    const passwordHash = await argon2.hash(dto.password);
    const { created, user } = await this.usersRepository.createUser({
      username: dto.username,
      email: dto.email,
      passwordHash,
      role: dto.role,
      active: true,
      mustChangePassword: true,
      tokenVersion: 0,
      createdBy: new Types.ObjectId(createdBy),
    });

    if (!created || !user) {
      throw new ConflictException('Username or email already in use');
    }

    return toUserResponse(user);
  }

  async updateByAdmin(
    id: string,
    dto: UpdateUserDto,
    requestingAdminId: string,
  ): Promise<UpdateByAdminResult> {
    if (id === requestingAdminId) {
      const wouldDeactivate = dto.active === false;
      const wouldDemote =
        dto.role !== undefined && dto.role !== UserRole.ADMINISTRATOR;
      if (wouldDeactivate || wouldDemote) {
        throw new ForbiddenException(
          'Cannot deactivate or demote your own account',
        );
      }
    }

    const existing = await this.usersRepository.findById(id);
    if (!existing) {
      throw new NotFoundException(`No user found with id ${id}`);
    }

    const changedFields: Record<string, unknown> = {};
    const changes: {
      email?: string;
      role?: UserRole;
      active?: boolean;
      passwordHash?: string;
    } = {};

    if (dto.email !== undefined) {
      changes.email = dto.email;
      changedFields.email = dto.email;
    }
    if (dto.role !== undefined) {
      changes.role = dto.role;
      changedFields.role = dto.role;
    }
    if (dto.active !== undefined) {
      changes.active = dto.active;
      changedFields.active = dto.active;
    }
    if (dto.password !== undefined) {
      changes.passwordHash = await argon2.hash(dto.password);
      changedFields.passwordReset = true;
    }

    let updated: UserDocument | null;
    try {
      updated = await this.usersRepository.applyAdminUpdate(id, changes);
    } catch (error) {
      if (isDuplicateKeyError(error)) {
        throw new ConflictException('Username or email already in use');
      }
      throw error;
    }

    if (!updated) {
      throw new NotFoundException(`No user found with id ${id}`);
    }

    return { user: toUserResponse(updated), changedFields };
  }

  /**
   * Seeds the default Administrator on first boot if no such user exists.
   * The seed password is never hardcoded: it comes from ADMIN_SEED_PASSWORD
   * and forces a mandatory change on first login (agent.md §9 DevOps rule).
   */
  async onModuleInit(): Promise<void> {
    const username = this.configService.get<string>('adminSeed.username');
    const email = this.configService.get<string>('adminSeed.email');
    const password = this.configService.get<string>('adminSeed.password');

    if (!username || !email || !password) {
      return;
    }

    const existing = await this.usersRepository.findByUsername(username);
    if (existing) {
      return;
    }

    const passwordHash = await argon2.hash(password);
    await this.usersRepository.create({
      username,
      email,
      passwordHash,
      role: UserRole.ADMINISTRATOR,
      active: true,
      mustChangePassword: true,
      tokenVersion: 0,
    });
    this.logger.log('Seeded default Administrator user', { username });
  }
}
