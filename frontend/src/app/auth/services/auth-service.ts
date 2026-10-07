import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { APP_CONFIG } from '../../core/config/app-config';
import { BrowserNavigation } from '../../core/services/browser-navigation';
import { ANONYMOUS_SESSION, SessionState } from '../models/session';

/**
 * Client-side view of the server-side session (BFF pattern, see ADR-0002).
 * The browser never sees access/refresh/id tokens: authentication state lives in an HttpOnly cookie
 * that JavaScript cannot read. This service is a UX helper, NOT a security boundary — the API
 * authorises every request.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly navigation = inject(BrowserNavigation);
  private readonly config = inject(APP_CONFIG);

  private readonly state = signal<SessionState>(ANONYMOUS_SESSION);

  readonly session = this.state.asReadonly();
  readonly isAuthenticated = computed(() => this.state().isAuthenticated);
  readonly user = computed(() => this.state().user);
  readonly isRoot = computed(() => this.state().isRoot);
  private readonly permissionSet = computed(() => new Set(this.state().permissions));

  /**
   * Whether the UI should offer an operation that requires `permission`. UX only (hide menus, disable buttons):
   * the API validates the permission again on every request (constitution §3.5).
   */
  hasPermission(permission: string): boolean {
    return this.state().isRoot || this.permissionSet().has(permission);
  }

  /** Loads the session once at start-up. Failures leave the app in the anonymous state. */
  async loadSession(): Promise<void> {
    try {
      const session = await firstValueFrom(
        this.http.get<SessionState>(`${this.config.authBaseUrl}/session`),
      );
      this.state.set(
        session.isAuthenticated
          ? { ...session, isRoot: !!session.isRoot, permissions: session.permissions ?? [] }
          : ANONYMOUS_SESSION,
      );
    } catch {
      this.state.set(ANONYMOUS_SESSION);
    }
  }

  /** Starts the Microsoft Entra ID sign-in (full-page redirect handled by the BFF). */
  login(returnUrl = '/'): void {
    const target = isLocalPath(returnUrl) ? returnUrl : '/';
    this.navigation.assign(
      `${this.config.authBaseUrl}/login?returnUrl=${encodeURIComponent(target)}`,
    );
  }

  /** Ends the local and Microsoft Entra ID sessions. */
  logout(): void {
    this.navigation.postForm(`${this.config.authBaseUrl}/logout`);
  }

  /** Called when the API reports 401: the server-side session expired or was revoked. */
  handleSessionExpired(): void {
    if (!this.isAuthenticated()) {
      return;
    }

    this.state.set(ANONYMOUS_SESSION);
    void this.router.navigate(['/login'], {
      queryParams: { reason: 'expired', returnUrl: this.router.url },
    });
  }
}

/** Only same-origin absolute paths are acceptable return targets (prevents open redirects). */
export function isLocalPath(url: string | null | undefined): url is string {
  return !!url && url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/\\');
}
