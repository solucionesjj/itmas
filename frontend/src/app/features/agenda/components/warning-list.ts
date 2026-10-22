import { Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { warningText } from '../data/agenda-labels';
import { Warning } from '../data/agenda.models';

/**
 * Non-blocking warnings of an activity (SPEC-0006 RN-11, §8.4): icon and text, never colour alone. They never prevent saving.
 */
@Component({
  selector: 'app-warning-list',
  imports: [MatIconModule],
  template: `
    @if (warnings().length > 0) {
      <ul class="warnings" role="status" aria-label="Advertencias">
        @for (warning of warnings(); track warning.eventId + warning.kind) {
          <li>
            <mat-icon aria-hidden="true">warning</mat-icon>
            <span>{{ text(warning) }}</span>
          </li>
        }
      </ul>
    }
  `,
  styles: `
    .warnings {
      list-style: none;
      margin: 0 0 16px;
      padding: 12px 16px;
      border-radius: var(--mat-sys-corner-medium);
      background: var(--mat-sys-tertiary-container);
      color: var(--mat-sys-on-tertiary-container);
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    li {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      font: var(--mat-sys-body-medium);
    }
    mat-icon {
      flex: none;
    }
  `,
})
export class WarningList {
  readonly warnings = input.required<readonly Warning[]>();

  protected readonly text = warningText;
}
