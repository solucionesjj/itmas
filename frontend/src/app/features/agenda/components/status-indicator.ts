import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { StatusPresentation } from '../data/agenda-labels';

/**
 * A state as icon + text (SPEC-0006 RNF-2, §11.1): the Material 3 colour role is applied to both, and the meaning is always
 * carried by the text, never by the colour alone.
 */
@Component({
  selector: 'app-agenda-status',
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
export class AgendaStatus {
  readonly status = input.required<StatusPresentation>();
}
