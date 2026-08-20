import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { I18nService } from '../../core/i18n/i18n.service';
import { MessageKey } from '../../core/i18n/messages.es-CO';
import { TranslatePipe } from '../../core/i18n/t.pipe';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    TranslatePipe
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);

  readonly form = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required]
  });

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  submit(): void {
    if (this.form.invalid || this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        const mustChangePassword = this.authService.currentUser()?.mustChangePassword;
        this.router.navigate([mustChangePassword ? '/change-password' : '/']);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(this.i18n.translate(LoginComponent.messageKeyFor(error)));
      }
    });
  }

  /**
   * §9.3: an error names the cause and the next step. The server's own message is
   * deliberately *not* passed through here — the API answers a bad login with the
   * English "Invalid credentials", which is neither localised nor actionable, and
   * §12 forbids a user-visible string that is not ours. Mapping by status keeps
   * the copy specific without leaking the wire language.
   *
   * Nothing here reveals whether the username exists: a wrong user and a wrong
   * password give the same answer, which is also what the backend does.
   */
  private static messageKeyFor(error: HttpErrorResponse): MessageKey {
    switch (error.status) {
      case 401:
        return 'login.errorInvalid';
      case 429:
        // The backend rate-limits login attempts (LOGIN_RATE_LIMIT_*).
        return 'login.errorThrottled';
      case 0:
        return 'login.errorOffline';
      default:
        return 'login.errorGeneric';
    }
  }
}
