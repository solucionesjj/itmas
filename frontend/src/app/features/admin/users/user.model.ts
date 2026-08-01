import { UserRole } from '../../../core/models/auth.models';

export interface User {
  _id: string;
  username: string;
  email: string;
  role: UserRole;
  active: boolean;
  createdBy?: string;
  createdAt: string;
  lastLogin?: string;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  role: UserRole;
}

export interface UpdateUserRequest {
  email?: string;
  role?: UserRole;
  active?: boolean;
  password?: string;
}
