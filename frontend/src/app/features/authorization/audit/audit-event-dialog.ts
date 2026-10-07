import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { AUDIT_ACTION_LABELS, AUDIT_RESULT_LABELS, AuditEvent } from '../data/authorization.models';

/** Full detail of an audit event, including the values before and after the change. */
@Component({
  selector: 'app-audit-event-dialog',
  imports: [DatePipe, MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ actionLabel }}</h2>
    <mat-dialog-content>
      <dl>
        <dt>Fecha</dt>
        <dd>{{ event.occurredAt | date: 'medium' }}</dd>
        <dt>Actor</dt>
        <dd>{{ event.actorDisplayName }}</dd>
        <dt>Recurso</dt>
        <dd>
          {{ event.resourceType }} <code>{{ event.resourceId ?? '—' }}</code>
        </dd>
        <dt>Resultado</dt>
        <dd>{{ resultLabel }}</dd>
        @if (event.traceId) {
          <dt>Referencia</dt>
          <dd>
            <code>{{ event.traceId }}</code>
          </dd>
        }
      </dl>
      <h3>Valores anteriores</h3>
      <pre>{{ pretty(event.before) }}</pre>
      <h3>Valores nuevos</h3>
      <pre>{{ pretty(event.after) }}</pre>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cerrar</button>
    </mat-dialog-actions>
  `,
  styles: `
    pre {
      white-space: pre-wrap;
      word-break: break-word;
      font-size: 0.8125rem;
      background: var(--mat-sys-surface-container);
      padding: 8px 12px;
      border-radius: 8px;
    }
    dt {
      font: var(--mat-sys-label-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    dd {
      margin: 0 0 8px;
    }
    h3 {
      font: var(--mat-sys-title-small);
    }
  `,
})
export class AuditEventDialog {
  protected readonly event = inject<AuditEvent>(MAT_DIALOG_DATA);
  protected readonly actionLabel = AUDIT_ACTION_LABELS[this.event.action] ?? this.event.action;
  protected readonly resultLabel = AUDIT_RESULT_LABELS[this.event.result] ?? this.event.result;

  protected pretty(json: string | null): string {
    if (!json) {
      return '—';
    }
    try {
      return JSON.stringify(JSON.parse(json), null, 2);
    } catch {
      return json;
    }
  }
}
