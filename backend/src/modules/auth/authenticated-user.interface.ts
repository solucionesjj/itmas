import { UserRole } from '../users/user-role.enum';

export interface AuthenticatedUser {
  sub: string;
  username: string;
  role: UserRole;
  mustChangePassword: boolean;
}

export interface RefreshTokenPayload {
  sub: string;
  tokenVersion: number;
}
