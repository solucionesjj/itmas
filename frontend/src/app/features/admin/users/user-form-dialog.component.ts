import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MessageKey } from '../../../core/i18n/messages.es-CO';
import { TranslatePipe } from '../../../core/i18n/t.pipe';
import { UserRole } from '../../../core/models/auth.models';
import { CreateUserRequest, UpdateUserRequest, User } from './user.model';

export interface UserFormDialogData {
  user?: User;
  isSelf: boolean;
}

export interface UserFormDialogResult {
  mode: 'create' | 'edit';
  create?: CreateUserRequest;
  update?: UpdateUserRequest;
}

const ROLES: { value: UserRole; labelKey: MessageKey }[] = [
  { value: 'administrator', labelKey: 'role.administrator' },
  { value: 'user', labelKey: 'role.user' },
  { value: 'auditor', labelKey: 'role.auditor' }
];

// Mirrors the backend's PASSWORD_POLICY_REGEX (min 8 chars, upper+lower+digit) —
// same client-side-parity rationale as ChangePasswordComponent.
const PASSWORD_POLICY_PATTERN = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/;

@Component({
  selector: 'app-user-form-dialog',
  standalone: true,
  imports: [TranslatePipe, 
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule
  ],
  templateUrl: './user-form-dialog.component.html',
  styleUrl: './user-form-dialog.component.scss'
})
export class UserFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef =
    inject<MatDialogRef<UserFormDialogComponent, UserFormDialogResult>>(
      MatDialogRef
    );
  protected readonly data = inject<UserFormDialogData>(MAT_DIALOG_DATA);

  protected readonly roles = ROLES;
  protected readonly isEdit = !!this.data.user;

  readonly form = this.fb.nonNullable.group({
    username: [
      { value: this.data.user?.username ?? '', disabled: this.isEdit },
      this.isEdit ? [] : [Validators.required, Validators.minLength(3)]
    ],
    email: [this.data.user?.email ?? '', [Validators.required, Validators.email]],
    password: [
      '',
      this.isEdit
        ? [Validators.pattern(PASSWORD_POLICY_PATTERN), Validators.minLength(8)]
        : [Validators.required, Validators.minLength(8), Validators.pattern(PASSWORD_POLICY_PATTERN)]
    ],
    role: [this.data.user?.role ?? ('user' as UserRole), Validators.required],
    active: [this.data.user?.active ?? true]
  });

  constructor() {
    // Editing yourself: don't offer a role/active change that would lock you
    // out. The backend rejects self-deactivation/self-demotion regardless —
    // this just avoids a confusing dead-end submit.
    if (this.isEdit && this.data.isSelf) {
      this.form.controls.role.disable();
      this.form.controls.active.disable();
    }
  }

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    const raw = this.form.getRawValue();

    if (!this.isEdit) {
      this.dialogRef.close({
        mode: 'create',
        create: {
          username: raw.username,
          email: raw.email,
          password: raw.password,
          role: raw.role
        }
      });
      return;
    }

    const update: UpdateUserRequest = {
      email: raw.email,
      role: raw.role,
      active: raw.active
    };
    if (raw.password) {
      update.password = raw.password;
    }
    this.dialogRef.close({ mode: 'edit', update });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
