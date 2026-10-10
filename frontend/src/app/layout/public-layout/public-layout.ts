import { Component, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatToolbar } from '@angular/material/toolbar';
import { RouterLink, RouterOutlet } from '@angular/router';
import { AuthService } from '../../auth/services/auth-service';
import { APP_CONFIG } from '../../core/config/app-config';
import { ThemeToggle } from '../theme-toggle/theme-toggle';

/**
 * Frame of the public pages (user manual, SPEC-0004): no session required. Offers the way into the application, or
 * back to it, using the session state the SPA already loads at start-up.
 */
@Component({
  selector: 'app-public-layout',
  imports: [MatToolbar, MatButton, MatIcon, RouterLink, RouterOutlet, ThemeToggle],
  template: `
    <mat-toolbar class="top-app-bar">
      <a class="app-name" routerLink="/manual">{{ appName }}</a>
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
      <router-outlet />
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
      top: 0;
      z-index: 2;
      gap: 8px;
    }
    .app-name {
      color: inherit;
      text-decoration: none;
    }
    .spacer {
      flex: 1 1 auto;
    }
    .content {
      padding: 24px 16px;
      box-sizing: border-box;
    }
  `,
})
export class PublicLayout {
  protected readonly appName = inject(APP_CONFIG).appName;
  protected readonly auth = inject(AuthService);
}
