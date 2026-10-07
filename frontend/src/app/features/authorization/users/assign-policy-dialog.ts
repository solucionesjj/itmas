import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { AuthorizationApi } from '../data/authorization-api';
import { PolicySummary } from '../data/authorization.models';

export interface AssignPolicyDialogData {
  readonly assignedPolicyIds: readonly string[];
}

/** Lets the administrator pick an active policy not yet assigned to the user. Returns the policy id. */
@Component({
  selector: 'app-assign-policy-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatProgressBarModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Asignar política</h2>
    <mat-dialog-content>
      @if (loading()) {
        <mat-progress-bar mode="indeterminate" aria-label="Cargando políticas" />
      } @else if (policies().length === 0) {
        <p>No hay políticas activas disponibles para asignar.</p>
      } @else {
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Política</mat-label>
          <mat-select [value]="selected()" (valueChange)="selected.set($event)">
            @for (policy of policies(); track policy.id) {
              <mat-option [value]="policy.id">
                {{ policy.name }} · {{ policy.permissionCount }} permisos
              </mat-option>
            }
          </mat-select>
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Cancelar</button>
      <button mat-flat-button [disabled]="!selected()" [mat-dialog-close]="selected()">
        Asignar
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .full-width {
      width: 100%;
      min-width: min(420px, 70vw);
    }
  `,
})
export class AssignPolicyDialog {
  private readonly data = inject<AssignPolicyDialogData>(MAT_DIALOG_DATA);

  protected readonly loading = signal(true);
  protected readonly policies = signal<readonly PolicySummary[]>([]);
  protected readonly selected = signal<string | null>(null);

  constructor() {
    inject(AuthorizationApi)
      .searchPolicies('', 'active', 1, 100)
      .subscribe({
        next: (page) => {
          this.policies.set(
            page.items.filter((policy) => !this.data.assignedPolicyIds.includes(policy.id)),
          );
          this.loading.set(false);
        },
        error: () => this.loading.set(false),
      });
  }
}
