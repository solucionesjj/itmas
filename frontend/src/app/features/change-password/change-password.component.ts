import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { I18nService } from '../../core/i18n/i18n.service';
import { MessageKey } from '../../core/i18n/messages.es-CO';
import { TranslatePipe } from '../../core/i18n/t.pipe';
import { AuthService } from '../../core/services/auth.service';

// Group-level validators (not per-control): both need to compare sibling
// field values, which a single-control Validators.* can't express.
function passwordsMatchValidator(group: AbstractControl): ValidationErrors | null {
  const newPassword = group.get('newPassword')?.value;
  const confirmNewPassword = group.get('confirmNewPassword')?.value;
  return newPassword === confirmNewPassword ? null : { passwordMismatch: true };
}

function newPasswordMustDifferValidator(group: AbstractControl): ValidationErrors | null {
  const currentPassword = group.get('currentPassword')?.value;
  const newPassword = group.get('newPassword')?.value;
  if (!currentPassword || !newPassword) {
    return null;
  }
  return currentPassword === newPassword ? { samePassword: true } : null;
}

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatButtonModule,
    TranslatePipe
  ],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss'
})
export class ChangePasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly i18n = inject(I18nService);

  // Mirrors the backend's PASSWORD_POLICY_REGEX (min 8 chars, upper+lower+digit)
  // so the user sees a validation error before submitting, not just a 400 back.
  private static readonly PASSWORD_POLICY_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/;

  readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', Validators.required],
      newPassword: [
        '',
        [
          Validators.required,
          Validators.minLength(8),
          Validators.pattern(ChangePasswordComponent.PASSWORD_POLICY_PATTERN)
        ]
      ],
      confirmNewPassword: ['', Validators.required]
    },
    { validators: [passwordsMatchValidator, newPasswordMustDifferValidator] }
  );

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  /**
   * §9.3: an error names the cause and the next step. The server's message is not
   * passed through — class-validator answers a policy violation in English
   * ("newPassword must be longer than or equal to 8 characters"), which §12
   * forbids surfacing. The client already enforces the same policy before submit,
   * so a 400 here means the two drifted and the copy says what the rule is.
   */
  private static messageKeyFor(error: HttpErrorResponse): MessageKey {
    switch (error.status) {
      case 400:
        return 'changePassword.errorPolicy';
      case 401:
        return 'changePassword.errorWrongCurrent';
      case 429:
        return 'changePassword.errorThrottled';
      case 0:
        return 'login.errorOffline';
      default:
        return 'changePassword.errorGeneric';
    }
  }

  submit(): void {
    if (this.form.invalid || this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const { currentPassword, newPassword } = this.form.getRawValue();

    this.authService.changePassword({ currentPassword, newPassword }).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/']);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(this.i18n.translate(ChangePasswordComponent.messageKeyFor(error)));
      }
    });
  }
}
