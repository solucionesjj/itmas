export type UserRole = 'administrator' | 'user' | 'auditor';

export interface JwtPayload {
  sub: string;
  username: string;
  role: UserRole;
  mustChangePassword: boolean;
  iat: number;
  exp: number;
}

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ApiErrorBody {
  error: {
    code: string;
    message: string;
    requestId: string;
  };
}
