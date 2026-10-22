import { Component, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { problemMessage } from '../../../core/models/problem-message';
import { AgendaApi } from '../data/agenda-api';
import { RESULTS, resultLabel } from '../data/agenda-labels';
import { localToInstant } from '../data/agenda-time';
import {
  Completion,
  CompletionInput,
  CompletionResult,
  LIMITS,
  VersionedEvent,
} from '../data/agenda.models';

export interface CompleteDialogData {
  readonly id: string;
  readonly title: string;
  readonly timeZone: string;
  /** Present when the execution was already recorded: then only the notes and the links can change (RN-9). */
  readonly completion: Completion | null;
}

/**
 * Records the execution of an activity and, afterwards, edits its notes and evidence links (SPEC-0006 RF-11, RN-9, RN-10).
 * Evidence is links only: no files and, above all, no secrets or findings (§8.4).
 */
@Component({
  selector: 'app-complete-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTooltipModule,
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ editing ? 'Editar notas y evidencia' : 'Registrar ejecución' }}
    </h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content class="content">
        <p class="lead">{{ data.title }}</p>
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Resultado</mat-label>
          <mat-select formControlName="result" required>
            @for (result of results; track result) {
              <mat-option [value]="result">{{ resultText(result) }}</mat-option>
            }
          </mat-select>
          @if (editing) {
            <mat-hint>El resultado ya registrado no se puede cambiar.</mat-hint>
          }
        </mat-form-field>
        @if (!editing) {
          <mat-form-field appearance="outline" subscriptSizing="dynamic">
            <mat-label>Fecha y hora reales de la ejecución (opcional)</mat-label>
            <input matInput type="datetime-local" formControlName="executedAt" />
            <mat-hint>Si la dejas vacía se usa el momento de este registro.</mat-hint>
          </mat-form-field>
        }
        <mat-form-field appearance="outline" subscriptSizing="dynamic">
          <mat-label>Notas (opcional)</mat-label>
          <textarea
            matInput
            formControlName="notes"
            rows="3"
            [attr.maxlength]="notesLimit"
          ></textarea>
          <mat-hint align="end">{{ form.controls.notes.value.length }}/{{ notesLimit }}</mat-hint>
        </mat-form-field>

        <fieldset class="links">
          <legend>Enlaces de evidencia</legend>
          @for (group of links.controls; track group; let index = $index) {
            <div class="link" [formGroup]="group">
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="label">
                <mat-label>Etiqueta</mat-label>
                <input matInput formControlName="label" [attr.maxlength]="labelLimit" />
              </mat-form-field>
              <mat-form-field appearance="outline" subscriptSizing="dynamic" class="url">
                <mat-label>URL (http o https)</mat-label>
                <input matInput formControlName="url" type="url" [attr.maxlength]="urlLimit" />
                @if (group.controls.url.hasError('pattern')) {
                  <mat-error>Debe ser una URL http o https sin usuario ni contraseña.</mat-error>
                }
              </mat-form-field>
              <button
                mat-icon-button
                type="button"
                matTooltip="Quitar enlace"
                [attr.aria-label]="'Quitar enlace ' + (index + 1)"
                (click)="removeLink(index)"
              >
                <mat-icon>delete</mat-icon>
              </button>
            </div>
          }
          <button
            mat-stroked-button
            type="button"
            [disabled]="links.length >= maxLinks"
            (click)="addLink()"
          >
            <mat-icon>add_link</mat-icon>
            Añadir enlace
          </button>
          <p class="hint">
            Solo enlaces: no se guardan archivos. No incluyas contraseñas, claves ni hallazgos de
            seguridad.
          </p>
        </fieldset>

        @if (error(); as message) {
          <p class="error" role="alert">{{ message }}</p>
        }
      </mat-dialog-content>
      @if (saving()) {
        <mat-progress-bar mode="indeterminate" aria-label="Guardando el registro" />
      }
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close [disabled]="saving()">Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid || saving()">
          {{ editing ? 'Guardar' : 'Registrar' }}
        </button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .content {
      display: flex;
      flex-direction: column;
      gap: 12px;
      min-width: min(560px, 80vw);
    }
    .lead {
      font: var(--mat-sys-title-small);
      margin: 0 0 8px;
      overflow-wrap: anywhere;
    }
    .links {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--mat-sys-corner-medium);
      padding: 12px 16px 16px;
      margin: 0 0 8px;
      display: flex;
      flex-direction: column;
      align-items: flex-start;
    }
    .links legend {
      font: var(--mat-sys-label-large);
      padding: 0 8px;
    }
    .link {
      display: flex;
      flex-wrap: wrap;
      gap: 0 8px;
      width: 100%;
      align-items: flex-start;
    }
    .link .label {
      flex: 1 1 160px;
    }
    .link .url {
      flex: 3 1 240px;
    }
    .hint {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
      margin: 12px 0 0;
    }
    .error {
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-medium);
      margin: 0;
    }
  `,
})
export class CompleteDialog {
  private readonly api = inject(AgendaApi);
  private readonly dialogRef = inject<MatDialogRef<CompleteDialog, VersionedEvent>>(MatDialogRef);
  protected readonly data = inject<CompleteDialogData>(MAT_DIALOG_DATA);

  protected readonly results = RESULTS;
  protected readonly resultText = resultLabel;
  protected readonly notesLimit = LIMITS.notes;
  protected readonly labelLimit = LIMITS.evidenceLabel;
  protected readonly urlLimit = LIMITS.evidenceUrl;
  protected readonly maxLinks = LIMITS.evidenceLinks;
  protected readonly editing = this.data.completion !== null;

  /** An http or https URL with no `user:password@` (RN-10), spaces around it allowed because they are trimmed on save; the API checks it again. */
  private static readonly URL = /^\s*https?:\/\/[^\s@/?#]+(?:[/?#]\S*)?\s*$/i;

  protected readonly form = new FormGroup({
    result: new FormControl<CompletionResult>(this.data.completion?.result ?? 'succeeded', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    executedAt: new FormControl('', { nonNullable: true }),
    notes: new FormControl(this.data.completion?.notes ?? '', {
      nonNullable: true,
      validators: [Validators.maxLength(LIMITS.notes)],
    }),
    evidenceLinks: new FormArray(
      (this.data.completion?.evidenceLinks ?? []).map((link) =>
        this.linkGroup(link.label, link.url),
      ),
    ),
  });

  protected get links() {
    return this.form.controls.evidenceLinks;
  }

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    if (this.editing) {
      this.form.controls.result.disable();
    }
  }

  protected addLink(): void {
    if (this.links.length < this.maxLinks) {
      this.links.push(this.linkGroup('', ''));
    }
  }

  protected removeLink(index: number): void {
    this.links.removeAt(index);
  }

  protected save(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const links = value.evidenceLinks
      .map((link) => ({ label: link.label.trim(), url: link.url.trim() }))
      .filter((link) => link.label !== '' || link.url !== '');
    const input: CompletionInput = {
      result: value.result,
      executedAt:
        !this.editing && value.executedAt
          ? localToInstant(value.executedAt, this.data.timeZone)
          : null,
      notes: value.notes.trim() || null,
      evidenceLinks: links,
    };

    this.saving.set(true);
    this.error.set(null);
    this.api.complete(this.data.id, input).subscribe({
      next: (result) => this.dialogRef.close(result),
      error: (error: unknown) => {
        this.saving.set(false);
        this.error.set(problemMessage(error));
      },
    });
  }

  private linkGroup(label: string, url: string) {
    return new FormGroup({
      label: new FormControl(label, {
        nonNullable: true,
        validators: [Validators.maxLength(LIMITS.evidenceLabel)],
      }),
      url: new FormControl(url, {
        nonNullable: true,
        validators: [
          Validators.maxLength(LIMITS.evidenceUrl),
          Validators.pattern(CompleteDialog.URL),
        ],
      }),
    });
  }
}
