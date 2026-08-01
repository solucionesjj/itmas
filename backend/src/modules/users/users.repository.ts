import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model, Types } from 'mongoose';
import { User, UserDocument } from './user.schema';
import { UserRole } from './user-role.enum';
import { isDuplicateKeyError } from '../../common/mongo/duplicate-key.util';

export interface CreateUserResult {
  created: boolean;
  user?: UserDocument;
}

export interface UsersListFilter {
  role?: UserRole;
  active?: boolean;
}

export interface AdminUserUpdate {
  email?: string;
  role?: UserRole;
  active?: boolean;
  passwordHash?: string;
}

@Injectable()
export class UsersRepository {
  constructor(
    @InjectModel(User.name) private readonly userModel: Model<UserDocument>,
  ) {}

  findByUsername(username: string): Promise<UserDocument | null> {
    return this.userModel.findOne({ username }).exec();
  }

  findById(id: string | Types.ObjectId): Promise<UserDocument | null> {
    return this.userModel.findById(id).exec();
  }

  findAll(filter: UsersListFilter = {}): Promise<UserDocument[]> {
    return this.userModel.find(filter).exec();
  }

  create(data: Partial<User>): Promise<UserDocument> {
    return this.userModel.create(data);
  }

  /** Used by `POST /users` — a duplicate username/email is a 409, not a 500. */
  async createUser(data: Partial<User>): Promise<CreateUserResult> {
    try {
      const user = await this.userModel.create(data);
      return { created: true, user };
    } catch (error) {
      if (isDuplicateKeyError(error)) {
        return { created: false };
      }
      throw error;
    }
  }

  /**
   * Used by `PATCH /users/:id`. A password reset always forces
   * mustChangePassword back to true and bumps tokenVersion (invalidates
   * outstanding refresh tokens) — same effect as self-service change-password,
   * just admin-triggered. Deactivating a user also bumps tokenVersion, so a
   * disabled account can't refresh its session (its current short-lived
   * access token is the only bounded residual access — this API is
   * intentionally stateless and JwtAuthGuard doesn't do a per-request DB
   * lookup, so that's an accepted, already-established tradeoff).
   */
  async applyAdminUpdate(
    id: string,
    changes: AdminUserUpdate,
  ): Promise<UserDocument | null> {
    const set: Record<string, unknown> = {};
    let bumpTokenVersion = false;

    if (changes.email !== undefined) {
      set.email = changes.email;
    }
    if (changes.role !== undefined) {
      set.role = changes.role;
    }
    if (changes.active !== undefined) {
      set.active = changes.active;
      if (changes.active === false) {
        bumpTokenVersion = true;
      }
    }
    if (changes.passwordHash !== undefined) {
      set.passwordHash = changes.passwordHash;
      set.mustChangePassword = true;
      bumpTokenVersion = true;
    }

    const update: Record<string, unknown> = {};
    if (Object.keys(set).length > 0) {
      update.$set = set;
    }
    if (bumpTokenVersion) {
      update.$inc = { tokenVersion: 1 };
    }
    if (Object.keys(update).length === 0) {
      return this.findById(id);
    }

    return this.userModel
      .findByIdAndUpdate(id, update, { returnDocument: 'after' })
      .exec();
  }

  async incrementTokenVersion(id: string | Types.ObjectId): Promise<void> {
    await this.userModel
      .updateOne({ _id: id }, { $inc: { tokenVersion: 1 } })
      .exec();
  }

  async setPasswordHash(
    id: string | Types.ObjectId,
    passwordHash: string,
  ): Promise<void> {
    await this.userModel
      .updateOne(
        { _id: id },
        {
          $set: { passwordHash, mustChangePassword: false },
          $inc: { tokenVersion: 1 },
        },
      )
      .exec();
  }

  async touchLastLogin(id: string | Types.ObjectId): Promise<void> {
    await this.userModel
      .updateOne({ _id: id }, { $set: { lastLogin: new Date() } })
      .exec();
  }
}
