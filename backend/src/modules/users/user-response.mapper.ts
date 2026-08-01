import { UserDocument } from './user.schema';
import { UserRole } from './user-role.enum';

export interface UserResponse {
  _id: string;
  username: string;
  email: string;
  role: UserRole;
  active: boolean;
  createdBy?: string;
  createdAt: Date;
  lastLogin?: Date;
  mustChangePassword: boolean;
}

/** Never return passwordHash/tokenVersion — internal fields, not API surface. */
export function toUserResponse(user: UserDocument): UserResponse {
  return {
    _id: user._id.toString(),
    username: user.username,
    email: user.email,
    role: user.role,
    active: user.active,
    createdBy: user.createdBy?.toString(),
    createdAt: user.createdAt,
    lastLogin: user.lastLogin,
    mustChangePassword: user.mustChangePassword,
  };
}
