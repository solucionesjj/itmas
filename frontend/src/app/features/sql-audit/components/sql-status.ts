import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { StatusPresentation } from '../data/sql-audit-labels';

/**
 * A state as icon + text (SPEC-0005 §11.1): the MD3 colour role is applied to both, and the meaning is always carried
 * by the text, never by the colour alone (RNF-7).
 */
@Component({
  selector: 'app-sql-status',
  imports: [MatIconModule],
  host: { '[class]': "'tone-' + status().tone" },
  template: `
    <mat-icon aria-hidden="true">{{ status().icon }}</mat-icon>
    <span>{{ status().label }}</span>
  `,
  styles: `
    :host {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      vertical-align: middle;
    }
    mat-icon {
      flex: none;
      font-size: 18px;
      width: 18px;
      height: 18px;
    }
    :host(.tone-error) {
      color: var(--mat-sys-error);
    }
    :host(.tone-tertiary) {
      color: var(--mat-sys-tertiary);
    }
    :host(.tone-primary) {
      color: var(--mat-sys-primary);
    }
    :host(.tone-on-surface-variant) {
      color: var(--mat-sys-on-surface-variant);
    }
    :host(.tone-outline) {
      color: var(--mat-sys-outline);
    }
  `,
})
export class SqlStatus {
  readonly status = input.required<StatusPresentation>();
}
