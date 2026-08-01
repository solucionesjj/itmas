import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuthTokens,
  ChangePasswordRequest,
  JwtPayload,
  LoginRequest
} from '../models/auth.models';
import { decodeJwt, isJwtExpired } from '../utils/jwt.util';

const ACCESS_TOKEN_KEY = 'itmas_access_token';
const REFRESH_TOKEN_KEY = 'itmas_refresh_token';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly payload = signal<JwtPayload | null>(this.readStoredPayload());

  readonly currentUser = computed(() => this.payload());
  readonly isAuthenticated = computed(() => !isJwtExpired(this.payload()));

  login(credentials: LoginRequest): Observable<AuthTokens> {
    return this.http
      .post<AuthTokens>(`${environment.apiBaseUrl}/auth/login`, credentials)
      .pipe(tap((tokens) => this.storeTokens(tokens)));
  }

  refresh(): Observable<AuthTokens> {
    const refreshToken = sessionStorage.getItem(REFRESH_TOKEN_KEY);
    return this.http
      .post<AuthTokens>(`${environment.apiBaseUrl}/auth/refresh`, { refreshToken })
      .pipe(tap((tokens) => this.storeTokens(tokens)));
  }

  logout(): Observable<void> {
    return this.http.post<void>(`${environment.apiBaseUrl}/auth/logout`, {}).pipe(
      tap({
        next: () => this.clearSession(),
        error: () => this.clearSession()
      })
    );
  }

  changePassword(request: ChangePasswordRequest): Observable<void> {
    return this.http
      .post<void>(`${environment.apiBaseUrl}/auth/change-password`, request)
      .pipe(tap(() => this.markPasswordChanged()));
  }

  getAccessToken(): string | null {
    return sessionStorage.getItem(ACCESS_TOKEN_KEY);
  }

  clearSession(): void {
    sessionStorage.removeItem(ACCESS_TOKEN_KEY);
    sessionStorage.removeItem(REFRESH_TOKEN_KEY);
    this.payload.set(null);
  }

  private storeTokens(tokens: AuthTokens): void {
    sessionStorage.setItem(ACCESS_TOKEN_KEY, tokens.accessToken);
    sessionStorage.setItem(REFRESH_TOKEN_KEY, tokens.refreshToken);
    this.payload.set(decodeJwt(tokens.accessToken));
  }

  private markPasswordChanged(): void {
    const current = this.payload();
    if (current) {
      this.payload.set({ ...current, mustChangePassword: false });
    }
  }

  private readStoredPayload(): JwtPayload | null {
    const token = sessionStorage.getItem(ACCESS_TOKEN_KEY);
    return token ? decodeJwt(token) : null;
  }
}
