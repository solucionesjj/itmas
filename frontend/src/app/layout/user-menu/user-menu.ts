import { Component, inject } from '@angular/core';
import { MatIconButton } from '@angular/material/button';
import { MatDivider } from '@angular/material/divider';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatTooltip } from '@angular/material/tooltip';
import { AuthService } from '../../auth/services/auth-service';

@Component({
  selector: 'app-user-menu',
  imports: [MatIconButton, MatDivider, MatIcon, MatMenu, MatMenuItem, MatMenuTrigger, MatTooltip],
  template: `
    <button
      mat-icon-button
      [matMenuTriggerFor]="userMenu"
      matTooltip="Cuenta"
      aria-label="Menú de la cuenta"
    >
      <mat-icon>account_circle</mat-icon>
    </button>
    <mat-menu #userMenu="matMenu" xPosition="before">
      @if (auth.user(); as user) {
        <div class="account" role="presentation">
          <span class="account-name">{{ user.displayName }}</span>
          @if (user.username) {
            <span class="account-username">{{ user.username }}</span>
          }
        </div>
        <mat-divider />
      }
      <button mat-menu-item (click)="auth.logout()">
        <mat-icon>logout</mat-icon>
        <span>Cerrar sesión</span>
      </button>
    </mat-menu>
  `,
  styles: `
    .account {
      display: flex;
      flex-direction: column;
      padding: 12px 16px;
    }
    .account-name {
      font: var(--mat-sys-title-small);
    }
    .account-username {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class UserMenu {
  protected readonly auth = inject(AuthService);
}
