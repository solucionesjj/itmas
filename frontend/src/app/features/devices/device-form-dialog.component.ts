import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { CreateDeviceRequest } from './device.model';

// Spanish labels reused verbatim from the devices-list filter's category
// select, so "Colaborador"/"Infraestructura" mean the same thing everywhere
// in the portal.
const CATEGORIES: { value: CreateDeviceRequest['category']; label: string }[] = [
  { value: 'collaborator', label: 'Colaborador' },
  { value: 'infrastructure', label: 'Infraestructura' }
];

@Component({
  selector: 'app-device-form-dialog',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule
  ],
  templateUrl: './device-form-dialog.component.html',
  styleUrl: './device-form-dialog.component.scss'
})
export class DeviceFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef =
    inject<MatDialogRef<DeviceFormDialogComponent, CreateDeviceRequest>>(
      MatDialogRef
    );

  protected readonly categories = CATEGORIES;

  readonly form = this.fb.nonNullable.group({
    hostname: ['', [Validators.required, Validators.minLength(1)]],
    category: ['collaborator' as CreateDeviceRequest['category'], Validators.required]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }
    this.dialogRef.close(this.form.getRawValue());
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
