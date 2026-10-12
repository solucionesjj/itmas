import { Component, computed, input, output } from '@angular/core';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ValidationStatus } from '../data/sql-audit.models';

export const NO_PERMISSION_HINT = 'No tienes permiso para validar accesos SQL.';
export const REMOVED_HINT = 'Eliminado del motor: ya no se puede validar.';

/**
 * "Válido" checkbox of an account, a role membership or a permission (RF-16). Usable with the keyboard; disabled with
 * an explanation without `sql-audit.accesses.validate` or when the element was removed. It stays focusable while
 * disabled so the explanation can be read.
 */
@Component({
  selector: 'app-validation-checkbox',
  imports: [MatCheckboxModule, MatTooltipModule],
  template: `
    <mat-checkbox
      [checked]="status() === 'valid'"
      [disabled]="disabled()"
      [disabledInteractive]="true"
      [attr.aria-busy]="saving() || null"
      [aria-label]="'Marcar como válido: ' + element()"
      [matTooltip]="hint()"
      [matTooltipDisabled]="!hint()"
      (change)="toggled.emit($event.checked)"
      >{{ label() }}</mat-checkbox
    >
  `,
})
export class ValidationCheckbox {
  /** Text that identifies the element for assistive technologies. */
  readonly element = input.required<string>();
  readonly status = input.required<ValidationStatus>();
  readonly canValidate = input.required<boolean>();
  readonly saving = input(false);
  /** Optional visible label (the tables use the column header instead). */
  readonly label = input('');

  readonly toggled = output<boolean>();

  protected readonly removed = computed(() => this.status() === 'removed');
  protected readonly disabled = computed(
    () => !this.canValidate() || this.removed() || this.saving(),
  );
  protected readonly hint = computed(() =>
    !this.canValidate() ? NO_PERMISSION_HINT : this.removed() ? REMOVED_HINT : '',
  );
}
