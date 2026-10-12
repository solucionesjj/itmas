import { Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { riskStatus } from '../data/sql-audit-labels';
import { RiskLevel } from '../data/sql-audit.models';
import { SqlStatus } from './sql-status';

/** Risk label (RF-14): a chip with the icon and the text of the level; "heredado" when it comes from roles (P-24). */
@Component({
  selector: 'app-risk-chip',
  imports: [MatChipsModule, SqlStatus],
  template: `
    <mat-chip-set [attr.aria-label]="'Riesgo: ' + text()">
      <mat-chip [disableRipple]="true">
        <app-sql-status [status]="status()" />
        @if (inherited()) {
          <span class="inherited"> (heredado)</span>
        }
      </mat-chip>
    </mat-chip-set>
  `,
  styles: `
    .inherited {
      white-space: pre;
      color: var(--mat-sys-on-surface-variant);
    }
  `,
})
export class RiskChip {
  readonly risk = input.required<RiskLevel>();
  /** The account's effective risk comes from the roles it is a member of. */
  readonly inherited = input(false);

  protected readonly status = computed(() => riskStatus(this.risk()));
  protected readonly text = computed(
    () => this.status().label + (this.inherited() ? ' (heredado)' : ''),
  );
}
