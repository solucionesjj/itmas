import { Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatToolbar } from '@angular/material/toolbar';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from '../../auth/services/auth-service';
import { APP_CONFIG } from '../../core/config/app-config';
import { Logo } from '../../shared/components/logo/logo';
import { ThemeToggle } from '../theme-toggle/theme-toggle';

/**
 * Frame of the public pages (user manual, SPEC-0004): no session required. Brand background and an opaque card around the
 * content (SPEC-0010 RF-9). Offers the way into the application, or
 * back to it, using the session state the SPA already loads at start-up.
 */
@Component({
  selector: 'app-public-layout',
  imports: [MatToolbar, MatButton, MatIcon, RouterLink, RouterOutlet, ThemeToggle, Logo],
  host: { class: 'itmas-brand-background' },
  template: `
    <mat-toolbar class="top-app-bar itmas-bar">
      <a class="app-name" routerLink="/manual"><app-logo /></a>
      <span class="spacer"></span>
      <app-theme-toggle />
      @if (auth.isAuthenticated()) {
        <a mat-button routerLink="/">
          <mat-icon>arrow_back</mat-icon>
          Volver a {{ appName }}
        </a>
      } @else {
        <a mat-button routerLink="/login">
          <mat-icon>login</mat-icon>
          Iniciar sesión
        </a>
      }
    </mat-toolbar>
    <main class="content">
      <div class="page itmas-opaque-card itmas-enter">
        <router-outlet />
      </div>
    </main>
  `,
  styles: `
    :host {
      display: flex;
      flex-direction: column;
      min-height: 100%;
    }
    .top-app-bar {
      position: sticky;
      top: 12px;
      z-index: 2;
      gap: 8px;
      box-sizing: border-box;
      width: auto;
      margin: 12px 12px 0;
    }
    .app-name {
      color: inherit;
      text-decoration: none;
    }
    .spacer {
      flex: 1 1 auto;
    }
    .content {
      padding: 24px 12px;
      box-sizing: border-box;
    }
    .page {
      box-sizing: border-box;
      max-width: 1040px;
      margin: 0 auto;
      padding: 24px 32px;
    }
    @media (max-width: 599px) {
      .page {
        padding: 16px;
      }
    }
  `,
})
export class PublicLayout {
  protected readonly appName = inject(APP_CONFIG).appName;
  protected readonly auth = inject(AuthService);
}
