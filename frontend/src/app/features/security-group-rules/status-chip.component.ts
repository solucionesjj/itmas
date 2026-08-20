import { Component, computed, input } from '@angular/core';
import { MessageKey } from '../../core/i18n/messages.es-CO';
import { TranslatePipe } from '../../core/i18n/t.pipe';
import { SecurityGroupRuleStatus } from './security-group-rule.model';

// Plain constant object, no framework abstraction — same style as the ROLES
// const in UserFormDialogComponent.
/** Message keys — the enum stays English-free in code, the label is translated (§12). */
const STATUS_LABEL_KEYS: Record<SecurityGroupRuleStatus, MessageKey> = {
  pendiente: 'status.pendiente',
  revisado: 'status.revisado',
  autorizado: 'status.autorizado',
  eliminado: 'status.eliminado'
};

/**
 * Maps a rule's workflow status onto one of design.md §9.7's four canonical badge
 * tones. The tones carry the colour (from the severity token pairs), so nothing
 * here knows a hex and both themes are handled by the token layer.
 *
 * `autorizado` is `online`, i.e. blue — §9.7 maps `online` to `--sev-low-*`, and
 * the SAC severity palette has no green. That is a deliberate call: the firewall
 * table loses the "green = compliant" read it used to have, in exchange for one
 * status vocabulary across the whole product. See BL-029.
 */
const STATUS_TONES: Record<SecurityGroupRuleStatus, string> = {
  pendiente: 'offline',
  revisado: 'degraded',
  autorizado: 'online',
  eliminado: 'unknown'
};

@Component({
  selector: 'app-status-chip',
  standalone: true,
  imports: [TranslatePipe],
  // A badge, not a chip: chips are interactive (filters), badges are read-only
  // state (§9.7). The previous `<mat-chip disabled>` was also announced as a
  // disabled control to screen readers, which it never was.
  template: `
    <span [class]="'badge badge--' + tone()">
      <span class="badge__dot" aria-hidden="true"></span>{{ labelKey() | t }}
    </span>
  `
})
export class StatusChipComponent {
  readonly status = input.required<SecurityGroupRuleStatus>();

  protected readonly labelKey = computed(() => STATUS_LABEL_KEYS[this.status()]);
  protected readonly tone = computed(() => STATUS_TONES[this.status()]);
}
