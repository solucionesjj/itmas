import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { problemMessage } from '../../../core/models/problem-message';
import { AgendaApi } from '../data/agenda-api';
import { LIMITS, VersionedEvent } from '../data/agenda.models';

export interface CancelDialogData {
  readonly id: string;
  readonly title: string;
  /** With Outlook active the guests are notified (§8.4). */
  readonly outlookEnabled: boolean;
}

/**
 * Cancels a scheduled activity with a mandatory reason (SPEC-0006 RF-10, RN-8, §8.4). The dialog saves by itself so that
 * a business-rule error is shown next to the form instead of closing it.
 */
@Component({
  selector: 'app-cancel-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule,
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>Cancelar actividad</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="content">
        <p class="lead">
          {{ data.title }}
        </p>
        <p class="supporting">
          @if (data.outlookEnabled) {
            Se cancelará la actividad y se avisará a los invitados.
          } @else {
            Se cancelará la actividad.
          }
          Una actividad cancelada no se puede volver a programar.
        </p>
        <mat-form-field appearance="outline">
          <mat-label>Motivo de la cancelación</mat-label>
          <textarea
            matInput
            formControlName="reason"
            rows="3"
            [attr.maxlength]="limit"
            required
            cdkFocusInitial
          ></textarea>
          <mat-hint align="end">{{ reason.value.length }}/{{ limit }}</mat-hint>
          @if (reason.hasError('required')) {
            <mat-error>El motivo es obligatorio.</mat-error>
          }
        </mat-form-field>
        @if (error(); as message) {
          <p class="error" role="alert">{{ message }}</p>
        }
      </mat-dialog-content>
      @if (saving()) {
        <mat-progress-bar mode="indeterminate" aria-label="Cancelando la actividad" />
      }
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close [disabled]="saving()">Volver</button>
        <button mat-flat-button type="submit" [disabled]="reason.invalid || saving()">
          Cancelar actividad
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .content {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: min(480px, 70vw);
    }
    .lead {
      font: var(--mat-sys-title-small);
      margin: 0;
      overflow-wrap: anywhere;
    }
    .supporting {
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
      margin: 0 0 12px;
    }
    .error {
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-medium);
      margin: 0;
    }
  `,
})
export class CancelDialog {
  private readonly api = inject(AgendaApi);
  private readonly dialogRef = inject<MatDialogRef<CancelDialog, VersionedEvent>>(MatDialogRef);
  protected readonly data = inject<CancelDialogData>(MAT_DIALOG_DATA);

  protected readonly limit = LIMITS.reason;
  protected readonly form = new FormGroup({
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(LIMITS.reason)],
    }),
  });
  protected readonly reason = this.form.controls.reason;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected save(): void {
    const reason = this.reason.value.trim();
    if (!reason || this.saving()) {
      this.reason.setErrors(reason ? null : { required: true });
      this.reason.markAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    this.api.cancel(this.data.id, reason).subscribe({
      next: (result) => this.dialogRef.close(result),
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(problemMessage(error));
      },
    });
  }
}
