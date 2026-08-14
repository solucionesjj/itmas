import { Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { SecurityGroupRuleStatus } from './security-group-rule.model';

// Plain constant object, no framework abstraction — same style as the ROLES
// const in UserFormDialogComponent. No existing status-color-chip pattern in
// this frontend; establishing it fresh here.
const STATUS_LABELS: Record<SecurityGroupRuleStatus, string> = {
  pendiente: 'Pendiente',
  revisado: 'Revisado',
  autorizado: 'Autorizado',
  eliminado: 'Eliminado'
};

const STATUS_COLORS: Record<SecurityGroupRuleStatus, string> = {
  pendiente: '#c62828',
  revisado: '#f9a825',
  autorizado: '#2e7d32',
  eliminado: '#212121'
};

@Component({
  selector: 'app-status-chip',
  standalone: true,
  imports: [MatChipsModule],
  template: `
    <mat-chip [style.background]="color()" [style.color]="'#fff'" disabled>
      {{ label() }}
    </mat-chip>
  `
})
export class StatusChipComponent {
  readonly status = input.required<SecurityGroupRuleStatus>();

  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
  protected readonly color = computed(() => STATUS_COLORS[this.status()]);
}
