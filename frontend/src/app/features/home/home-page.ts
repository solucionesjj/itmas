import { Component, inject } from '@angular/core';
import { MatCard, MatCardContent, MatCardHeader, MatCardTitle } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import {
  MatList,
  MatListItem,
  MatListItemIcon,
  MatListItemLine,
  MatListItemTitle,
} from '@angular/material/list';
import { AuthService } from '../../auth/services/auth-service';

@Component({
  selector: 'app-home-page',
  imports: [
    MatCard,
    MatCardContent,
    MatCardHeader,
    MatCardTitle,
    MatIcon,
    MatList,
    MatListItem,
    MatListItemIcon,
    MatListItemLine,
    MatListItemTitle,
  ],
  template: `
    <h1 class="headline">Hola, {{ auth.user()?.displayName }}</h1>
    <p class="supporting-text">
      Bienvenido. Desde aquí accederás a las funcionalidades de la aplicación.
    </p>

    <mat-card appearance="outlined" class="account-card">
      <mat-card-header>
        <mat-card-title>Tu cuenta</mat-card-title>
      </mat-card-header>
      <mat-card-content>
        <mat-list>
          <mat-list-item>
            <mat-icon matListItemIcon>badge</mat-icon>
            <span matListItemTitle>Nombre</span>
            <span matListItemLine>{{ auth.user()?.displayName }}</span>
          </mat-list-item>
          @if (auth.user()?.username; as username) {
            <mat-list-item>
              <mat-icon matListItemIcon>alternate_email</mat-icon>
              <span matListItemTitle>Cuenta</span>
              <span matListItemLine>{{ username }}</span>
            </mat-list-item>
          }
        </mat-list>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .headline {
      font: var(--mat-sys-headline-medium);
      margin: 0 0 8px;
    }
    .supporting-text {
      font: var(--mat-sys-body-large);
      color: var(--mat-sys-on-surface-variant);
      margin: 0 0 24px;
    }
    .account-card {
      max-width: 560px;
    }
  `,
})
export class HomePage {
  protected readonly auth = inject(AuthService);
}
