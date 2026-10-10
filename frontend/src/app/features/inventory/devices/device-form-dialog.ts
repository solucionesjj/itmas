import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Observable } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { problemMessage } from '../../../core/models/problem-message';
import { InventoryApi } from '../data/inventory-api';
import { DeviceCreated, DeviceDetail, DeviceInput, DeviceType } from '../data/inventory.models';

export type DeviceFormDialogData =
  | { readonly mode: 'create' }
  | { readonly mode: 'edit'; readonly device: DeviceDetail; readonly etag: string };

export type DeviceFormDialogResult = DeviceCreated | DeviceDetail;

const DUPLICATE_NAME = 'Ya existe un equipo con este nombre.';

/**
 * Creates or edits a device (SPEC-0003 RF-1, RF-4). The dialog saves by itself so that business-rule errors
 * (duplicate name, invalid e-mail, concurrent change) are shown next to the form instead of closing it.
 */
@Component({
  selector: 'app-device-form-dialog',
  imports: [
    MatButtonModule,
    MatButtonToggleModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.mode === 'create' ? 'Nuevo equipo' : 'Editar equipo' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="content">
        <mat-form-field appearance="outline">
          <mat-label>Nombre del equipo</mat-label>
          <input matInput formControlName="name" maxlength="100" required cdkFocusInitial />
          @if (form.controls.name.hasError('duplicate')) {
            <mat-error>{{ duplicateName }}</mat-error>
          } @else if (form.controls.name.hasError('required')) {
            <mat-error>El nombre es obligatorio.</mat-error>
          }
        </mat-form-field>

        <div class="type">
          <span id="device-type-label" class="type-label">Tipo de equipo</span>
          <mat-button-toggle-group
            formControlName="type"
            aria-labelledby="device-type-label"
            [hideSingleSelectionIndicator]="false"
          >
            <mat-button-toggle value="workstation">Estación de trabajo</mat-button-toggle>
            <mat-button-toggle value="server">Servidor</mat-button-toggle>
          </mat-button-toggle-group>
        </div>

        <mat-form-field appearance="outline">
          <mat-label>Correo del propietario (opcional)</mat-label>
          <input matInput type="email" formControlName="ownerEmail" maxlength="254" />
          <mat-hint>Si coincide con un usuario de ITMas, se mostrará su nombre.</mat-hint>
          @if (form.controls.ownerEmail.hasError('email')) {
            <mat-error>Introduce un correo válido.</mat-error>
          }
        </mat-form-field>

        @if (error(); as error) {
          <p class="error" role="alert">{{ error }}</p>
        }
      </mat-dialog-content>
      @if (saving()) {
        <mat-progress-bar mode="indeterminate" aria-label="Guardando equipo" />
      }
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close [disabled]="saving()">Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || saving()">
          {{ data.mode === 'create' ? 'Crear' : 'Guardar' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .content {
      display: flex;
      flex-direction: column;
      gap: 8px;
      min-width: min(440px, 70vw);
    }
    .type {
      display: flex;
      flex-direction: column;
      gap: 8px;
      margin-bottom: 16px;
    }
    .type-label {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
    }
    .error {
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-medium);
      margin: 0;
    }
  `,
})
export class DeviceFormDialog {
  private readonly api = inject(InventoryApi);
  private readonly dialogRef =
    inject<MatDialogRef<DeviceFormDialog, DeviceFormDialogResult>>(MatDialogRef);
  protected readonly data = inject<DeviceFormDialogData>(MAT_DIALOG_DATA);
  protected readonly duplicateName = DUPLICATE_NAME;

  private readonly current = this.data.mode === 'edit' ? this.data.device : null;

  protected readonly form = new FormGroup({
    name: new FormControl(this.current?.name ?? '', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    type: new FormControl<DeviceType>(this.current?.type ?? 'workstation', { nonNullable: true }),
    ownerEmail: new FormControl(this.current?.owner?.email ?? '', {
      nonNullable: true,
      validators: [Validators.email, Validators.maxLength(254)],
    }),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected save(): void {
    const value = this.form.getRawValue();
    const name = value.name.trim();
    if (this.form.invalid || this.saving() || !name) {
      this.form.controls.name.setErrors(name ? null : { required: true });
      return;
    }

    const input: DeviceInput = {
      name,
      type: value.type,
      ownerEmail: value.ownerEmail.trim() || null,
    };
    const request: Observable<DeviceFormDialogResult> =
      this.data.mode === 'create'
        ? this.api.createDevice(input)
        : this.api.updateDevice(this.data.device.id, this.data.etag, input);

    this.saving.set(true);
    this.error.set(null);
    request.subscribe({
      next: (result) => this.dialogRef.close(result),
      error: (error: unknown) => {
        this.saving.set(false);
        const message = problemMessage(error);
        if (error instanceof ApiError && error.status === 422 && message === DUPLICATE_NAME) {
          this.form.controls.name.setErrors({ duplicate: true });
          this.form.controls.name.markAsTouched();
        } else {
          this.error.set(message);
        }
      },
    });
  }
}
