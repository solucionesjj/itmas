import { Component, computed, inject, input } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { AuthService } from '../../auth/services/auth-service';
import { APP_CONFIG } from '../../core/config/app-config';
import { StatusMessage } from '../../shared/components/status-message/status-message';

@Component({
  selector: 'app-login-page',
  imports: [MatButton, MatIcon, StatusMessage],
  template: `
    <app-status-message icon="lock" [title]="appName" [description]="message()">
      <button actions mat-flat-button (click)="signIn()">
        <mat-icon>login</mat-icon>
        Iniciar sesión con Microsoft
      </button>
    </app-status-message>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
    }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  protected readonly appName = inject(APP_CONFIG).appName;

  /** Bound from the query string (withComponentInputBinding). */
  readonly reason = input<string>();
  readonly error = input<string>();
  readonly returnUrl = input<string>();

  protected readonly message = computed(() => {
    switch (this.error()) {
      case 'unavailable':
        return 'El inicio de sesión no está disponible en este momento.';
      case 'disabled':
        return 'Tu cuenta está desactivada. Contacta con un administrador.';
      case 'guest':
        return 'Las cuentas invitadas no tienen acceso a esta aplicación.';
    }
    switch (this.reason()) {
      case 'expired':
        return 'Tu sesión ha expirado. Inicia sesión de nuevo para continuar.';
      case 'signed-out':
        return 'Has cerrado sesión correctamente.';
      default:
        return 'Inicia sesión con tu cuenta corporativa para continuar.';
    }
  });

  protected signIn(): void {
    this.auth.login(this.returnUrl() ?? '/');
  }
}
