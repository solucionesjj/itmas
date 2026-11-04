import { Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { statusClasses } from '../../../shared/models/status-tone';
import { StatusPresentation } from '../data/agenda-labels';

/**
 * A state as icon + text (SPEC-0006 RNF-2, §11.1) on a tinted label of its tone (SPEC-0010 RF-8, painted by the theme layer);
 * the meaning is always carried by the text, never by the colour alone.
 */
@Component({
  selector: 'app-agenda-status',
  imports: [MatIconModule],
  host: { '[class]': 'classes()' },
  template: `
    <mat-icon aria-hidden="true">{{ status().icon }}</mat-icon>
    <span>{{ status().label }}</span>
  `,
  styles: `
    mat-icon {
      flex: none;
      font-size: var(--itmas-icon-size-sm);
      width: var(--itmas-icon-size-sm);
      height: var(--itmas-icon-size-sm);
    }
  `,
})
export class AgendaStatus {
  readonly status = input.required<StatusPresentation>();

  protected readonly classes = computed(() => statusClasses(this.status().tone));
}
