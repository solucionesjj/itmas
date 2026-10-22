import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { ApiError } from '../../../core/models/api-error';
import { problemMessage } from '../../../core/models/problem-message';
import { AgendaApi, etagOf } from '../data/agenda-api';
import { EventType, EventTypeInput, LIMITS } from '../data/agenda.models';

export interface TypeDialogData {
  /** The type to edit, or `null` to create one. */
  readonly type: EventType | null;
}

const integer = (control: { value: unknown }) =>
  control.value === null || control.value === '' || Number.isInteger(Number(control.value))
    ? null
    : { integer: true };

/**
 * Creates or edits an activity type (SPEC-0006 RF-1, RF-2). The dialog saves by itself so that a business-rule error (a
 * repeated name, a figure out of range) shows next to the form instead of closing it. A type is deactivated, never deleted.
 */
@Component({
  selector: 'app-type-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ editing ? 'Editar tipo de actividad' : 'Nuevo tipo de actividad' }}</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="content">
        <mat-form-field appearance="outline">
          <mat-label>Nombre</mat-label>
          <input
            matInput
            formControlName="name"
            [attr.maxlength]="limits.typeName"
            required
            cdkFocusInitial
          />
          @if (form.controls.name.hasError('required')) {
            <mat-error>El nombre es obligatorio.</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Descripción (opcional)</mat-label>
          <textarea
            matInput
            formControlName="description"
            rows="2"
            [attr.maxlength]="limits.typeDescription"
          ></textarea>
        </mat-form-field>
        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Duración por defecto (minutos)</mat-label>
            <input
              matInput
              type="number"
              formControlName="defaultDurationMinutes"
              min="1"
              [attr.max]="limits.maxDurationMinutes"
              step="1"
              required
            />
            @if (form.controls.defaultDurationMinutes.invalid) {
              <mat-error>Un número entero de 1 a {{ limits.maxDurationMinutes }}.</mat-error>
            }
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Recordatorio por defecto (minutos antes)</mat-label>
            <input
              matInput
              type="number"
              formControlName="defaultReminderMinutes"
              min="0"
              [attr.max]="limits.maxReminderMinutes"
              step="1"
              required
            />
            @if (form.controls.defaultReminderMinutes.invalid) {
              <mat-error>Un número entero de 0 a {{ limits.maxReminderMinutes }}.</mat-error>
            }
          </mat-form-field>
        </div>
        <mat-slide-toggle formControlName="isFreeze">
          Periodo de congelamiento (no deben programarse cambios)
        </mat-slide-toggle>
        @if (editing) {
          <mat-slide-toggle formControlName="isActive">
            Activo (un tipo inactivo no se puede asignar a actividades nuevas)
          </mat-slide-toggle>
        }
        @if (error(); as message) {
          <p class="error" role="alert">{{ message }}</p>
        }
      </mat-dialog-content>
      @if (saving()) {
        <mat-progress-bar mode="indeterminate" aria-label="Guardando el tipo" />
      }
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close [disabled]="saving()">Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="saving()">
          {{ editing ? 'Guardar' : 'Crear tipo' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .content {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: min(560px, 80vw);
    }
    .row {
      display: flex;
      flex-wrap: wrap;
      gap: 0 16px;
    }
    .row mat-form-field {
      flex: 1 1 220px;
    }
    mat-slide-toggle {
      margin-bottom: 12px;
    }
    .error {
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-medium);
      margin: 0;
    }
  `,
})
export class TypeDialog {
  private readonly api = inject(AgendaApi);
  private readonly dialogRef = inject<MatDialogRef<TypeDialog, EventType>>(MatDialogRef);
  protected readonly data = inject<TypeDialogData>(MAT_DIALOG_DATA);

  protected readonly limits = LIMITS;
  protected readonly editing = this.data.type !== null;

  protected readonly form = new FormGroup({
    name: new FormControl(this.data.type?.name ?? '', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(LIMITS.typeName)],
    }),
    description: new FormControl(this.data.type?.description ?? '', {
      nonNullable: true,
      validators: [Validators.maxLength(LIMITS.typeDescription)],
    }),
    defaultDurationMinutes: new FormControl<number | null>(
      this.data.type?.defaultDurationMinutes ?? 60,
      {
        validators: [
          Validators.required,
          Validators.min(1),
          Validators.max(LIMITS.maxDurationMinutes),
          integer,
        ],
      },
    ),
    defaultReminderMinutes: new FormControl<number | null>(
      this.data.type?.defaultReminderMinutes ?? 1440,
      {
        validators: [
          Validators.required,
          Validators.min(0),
          Validators.max(LIMITS.maxReminderMinutes),
          integer,
        ],
      },
    ),
    isFreeze: new FormControl(this.data.type?.isFreeze ?? false, { nonNullable: true }),
    isActive: new FormControl(this.data.type?.isActive ?? true, { nonNullable: true }),
  });

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected save(): void {
    if (this.saving()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const input: EventTypeInput = {
      name: value.name.trim(),
      description: value.description.trim() || null,
      defaultDurationMinutes: Number(value.defaultDurationMinutes),
      defaultReminderMinutes: Number(value.defaultReminderMinutes),
      isFreeze: value.isFreeze,
      ...(this.editing ? { isActive: value.isActive } : {}),
    };

    this.saving.set(true);
    this.error.set(null);
    const type = this.data.type;
    const request = type
      ? this.api.updateType(type.id, etagOf(type.revision), input)
      : this.api.createType(input);
    request.subscribe({
      next: (saved) => this.dialogRef.close(saved),
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(
          error instanceof ApiError && error.status === 412
            ? 'Otro usuario modificó este tipo. Cierra el diálogo y vuelve a abrirlo para ver los cambios.'
            : problemMessage(error),
        );
      },
    });
  }
}
