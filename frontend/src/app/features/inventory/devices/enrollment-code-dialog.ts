import { ClipboardModule } from '@angular/cdk/clipboard';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { Observable } from 'rxjs';
import { NotificationService } from '../../../core/services/notification-service';

export interface EnrollmentCodeDialogData {
  readonly deviceName: string;
  readonly code: string;
}

/**
 * Shows a device enrollment code the only time it is available (ADR-0012). The code lives only in this dialog: it is
 * not stored in any service, signal or browser storage, and closing the dialog discards it.
 */
@Component({
  selector: 'app-enrollment-code-dialog',
  imports: [ClipboardModule, MatButtonModule, MatDialogModule, MatIconModule],
  template: `
    <h2 mat-dialog-title>Código del equipo</h2>
    <mat-dialog-content class="content">
      <p>
        Código de enrolamiento de <strong>{{ data.deviceName }}</strong
        >. Introdúcelo al instalar el agente (<code>itmas-agent install</code>).
      </p>
      <p class="code" aria-label="Código de enrolamiento">{{ data.code }}</p>
      <p class="warning">
        <mat-icon aria-hidden="true">warning</mat-icon>
        <span>
          Solo se muestra ahora y sirve una sola vez. Cópialo y entrégalo de forma segura a quien
          instalará el agente. Si se pierde, regenera el código.
        </span>
      </p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button
        mat-stroked-button
        type="button"
        [cdkCopyToClipboard]="data.code"
        (cdkCopyToClipboardCopied)="copied($event)"
      >
        <mat-icon>content_copy</mat-icon>
        Copiar código
      </button>
      <button mat-flat-button type="button" mat-dialog-close>Listo</button>
    </mat-dialog-actions>
  `,
  styles: `
    .content {
      max-width: 520px;
    }
    .code {
      font-family: ui-monospace, SFMono-Regular, Menlo, monospace;
      font-size: 1.5rem;
      letter-spacing: 0.08em;
      text-align: center;
      padding: 16px;
      border-radius: 8px;
      background: var(--mat-sys-surface-container-high);
      user-select: all;
      overflow-wrap: anywhere;
    }
    .warning {
      display: flex;
      gap: 8px;
      align-items: flex-start;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class EnrollmentCodeDialog {
  private readonly notifications = inject(NotificationService);
  protected readonly data = inject<EnrollmentCodeDialogData>(MAT_DIALOG_DATA);

  protected copied(success: boolean): void {
    if (success) {
      this.notifications.info('Código copiado.');
    } else {
      this.notifications.error('No se pudo copiar. Selecciona el código y cópialo a mano.');
    }
  }
}

/** Opens the code dialog; it can only be dismissed with "Listo", so the code is not lost by an accidental click. */
export function showEnrollmentCode(
  dialog: MatDialog,
  data: EnrollmentCodeDialogData,
): Observable<void> {
  return dialog
    .open<EnrollmentCodeDialog, EnrollmentCodeDialogData, void>(EnrollmentCodeDialog, {
      data,
      disableClose: true,
    })
    .afterClosed();
}
