import { Prop, Schema, SchemaFactory } from '@nestjs/mongoose';
import { HydratedDocument, Types } from 'mongoose';
import { UserRole } from './user-role.enum';

export type UserDocument = HydratedDocument<User>;

@Schema({ collection: 'users' })
export class User {
  _id!: Types.ObjectId;

  @Prop({ required: true, unique: true, trim: true })
  username!: string;

  @Prop({ required: true, unique: true, trim: true, lowercase: true })
  email!: string;

  @Prop({ required: true })
  passwordHash!: string;

  @Prop({ type: String, required: true, enum: UserRole })
  role!: UserRole;

  @Prop({ required: true, default: true })
  active!: boolean;

  @Prop({ type: Types.ObjectId, ref: 'User', required: false })
  createdBy?: Types.ObjectId;

  @Prop({ required: true, default: () => new Date() })
  createdAt!: Date;

  @Prop({ required: false })
  lastLogin?: Date;

  // Additive fields not in spec.md's example payload (see agent.md ADR note):
  // forces a password change before other actions, set on seed/admin-creation.
  @Prop({ required: true, default: false })
  mustChangePassword!: boolean;

  // Incremented on logout/change-password to invalidate outstanding refresh tokens
  // (revocation mechanism referenced by agent.md §6.3, no separate collection needed).
  @Prop({ required: true, default: 0 })
  tokenVersion!: number;
}

export const UserSchema = SchemaFactory.createForClass(User);
// Indexes already declared via `unique: true` on the @Prop() fields above
// (spec.md §5.3: users.username unique, users.email unique).
