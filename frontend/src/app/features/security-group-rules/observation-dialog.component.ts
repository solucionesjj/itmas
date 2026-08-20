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
import { TranslatePipe } from '../../core/i18n/t.pipe';

export interface ObservationDialogData {
  title: string;
  actionLabel: string;
}

export interface ObservationDialogResult {
  observation: string;
}

// Generic dialog reused for both "Marcar como revisado" and "Marcar como
// autorizado" (same shape, different title/verb via dialog data) — built on
// the same MatDialogRef<T, Result> + MAT_DIALOG_DATA template as
// UserFormDialogComponent.
@Component({
  selector: 'app-observation-dialog',
  standalone: true,
  imports: [TranslatePipe, 
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule
  ],
  templateUrl: './observation-dialog.component.html',
  styleUrl: './observation-dialog.component.scss'
})
export class ObservationDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef =
    inject<MatDialogRef<ObservationDialogComponent, ObservationDialogResult>>(
      MatDialogRef
    );
  protected readonly data = inject<ObservationDialogData>(MAT_DIALOG_DATA);

  readonly form = this.fb.nonNullable.group({
    observation: ['', [Validators.required, Validators.minLength(1)]]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }
    this.dialogRef.close({ observation: this.form.getRawValue().observation });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
