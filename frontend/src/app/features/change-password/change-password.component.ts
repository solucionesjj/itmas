import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule
  ],
  templateUrl: './change-password.component.html',
  styleUrl: './change-password.component.scss'
})
export class ChangePasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  // Mirrors the backend's PASSWORD_POLICY_REGEX (min 8 chars, upper+lower+digit)
  // so the user sees a validation error before submitting, not just a 400 back.
  private static readonly PASSWORD_POLICY_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/;

  readonly form = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: [
      '',
      [
        Validators.required,
        Validators.minLength(8),
        Validators.pattern(ChangePasswordComponent.PASSWORD_POLICY_PATTERN)
      ]
    ]
  });

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  submit(): void {
    if (this.form.invalid || this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.changePassword(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/']);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(
          error.error?.error?.message ?? 'No se pudo cambiar la contraseña.'
        );
      }
    });
  }
}
