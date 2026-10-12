import { DatePipe } from '@angular/common';
import { Component, computed, inject, input, output } from '@angular/core';
import { MatSortModule, Sort, SortDirection } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import {
  STALE_STATUS,
  accessElement,
  accountName,
  levelLabel,
  securableKind,
  securablePath,
  sqlLabel,
  validationStatus,
} from '../data/sql-audit-labels';
import { SqlAccessItem } from '../data/sql-audit.models';
import { SqlValidation, currentStatus } from '../data/sql-validation';
import { RiskChip } from './risk-chip';
import { SqlStatus } from './sql-status';
import { ValidationCheckbox } from './validation-checkbox';

export type AccessColumn =
  | 'account'
  | 'role'
  | 'securable'
  | 'permission'
  | 'state'
  | 'grantor'
  | 'risk'
  | 'status'
  | 'firstSeenAt'
  | 'valid';

export const PERMISSION_COLUMNS: readonly AccessColumn[] = [
  'securable',
  'permission',
  'state',
  'grantor',
  'risk',
  'status',
  'firstSeenAt',
  'valid',
];
export const ROLE_COLUMNS: readonly AccessColumn[] = [
  'role',
  'risk',
  'status',
  'firstSeenAt',
  'valid',
];

export interface AccessToggle {
  readonly item: SqlAccessItem;
  readonly valid: boolean;
}

/**
 * Table of role memberships or permissions with risk, validation state and "Válido" checkbox (RF-22, RF-23). Paging,
 * filters and the optimistic update belong to the page; sorting is server-side when `sortable`.
 */
@Component({
  selector: 'app-access-table',
  imports: [
    DatePipe,
    MatSortModule,
    MatTableModule,
    RiskChip,
    RouterLink,
    SqlStatus,
    ValidationCheckbox,
  ],
  template: `
    <div class="table-container">
      <table
        mat-table
        matSort
        [matSortDisabled]="!sortable()"
        [matSortActive]="sortActive()"
        [matSortDirection]="sortDirection()"
        [dataSource]="items()"
        (matSortChange)="sortChange.emit($event)"
        [attr.aria-label]="tableLabel()"
      >
        <ng-container matColumnDef="account">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Cuenta</th>
          <td mat-cell *matCellDef="let item">
            @if (item.account; as account) {
              <a [routerLink]="['/sql-audit/accounts', account.id]">{{ accountName(account) }}</a>
              <span class="secondary">{{ levelLabel(account.level, account.database) }}</span>
            } @else {
              —
            }
          </td>
        </ng-container>

        <ng-container matColumnDef="role">
          <th mat-header-cell *matHeaderCellDef>Rol</th>
          <td mat-cell *matCellDef="let item">
            @if (item.role; as role) {
              <a [routerLink]="['/sql-audit/accounts', role.accountId]">{{ role.name }}</a>
            }
            @if (item.stale) {
              <span class="markers"><app-sql-status [status]="staleStatus" /></span>
            }
          </td>
        </ng-container>

        <ng-container matColumnDef="securable">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Objeto</th>
          <td mat-cell *matCellDef="let item">
            {{ securablePath(item.securable) }}
            <span class="secondary">{{ securableKind(item.securable) }}</span>
            @if (item.stale) {
              <span class="markers"><app-sql-status [status]="staleStatus" /></span>
            }
          </td>
        </ng-container>

        <ng-container matColumnDef="permission">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Permiso</th>
          <td mat-cell *matCellDef="let item">{{ item.permission }}</td>
        </ng-container>

        <ng-container matColumnDef="state">
          <th mat-header-cell *matHeaderCellDef>Estado</th>
          <td mat-cell *matCellDef="let item">{{ label('state', item.state) }}</td>
        </ng-container>

        <ng-container matColumnDef="grantor">
          <th mat-header-cell *matHeaderCellDef>Concedido por</th>
          <td mat-cell *matCellDef="let item">{{ item.grantor ?? '—' }}</td>
        </ng-container>

        <ng-container matColumnDef="risk">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Riesgo</th>
          <td mat-cell *matCellDef="let item"><app-risk-chip [risk]="item.risk" /></td>
        </ng-container>

        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Validación</th>
          <td mat-cell *matCellDef="let item">
            <app-sql-status [status]="validationStatus(currentStatus(item))" />
          </td>
        </ng-container>

        <ng-container matColumnDef="firstSeenAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Detectado</th>
          <td mat-cell *matCellDef="let item">{{ item.firstSeenAt | date: 'mediumDate' }}</td>
        </ng-container>

        <ng-container matColumnDef="valid">
          <th mat-header-cell *matHeaderCellDef class="check-cell">Válido</th>
          <td mat-cell *matCellDef="let item" class="check-cell">
            <app-validation-checkbox
              [element]="accessElement(item)"
              [status]="currentStatus(item)"
              [canValidate]="canValidate()"
              [saving]="validation.isSaving(item.id)"
              (toggled)="toggled.emit({ item, valid: $event })"
            />
          </td>
        </ng-container>

        <tr mat-header-row *matHeaderRowDef="columnList()"></tr>
        <tr mat-row *matRowDef="let row; columns: columnList()"></tr>
        <tr class="mat-mdc-no-data-row" *matNoDataRow>
          <td class="empty" [attr.colspan]="columnList().length">{{ emptyText() }}</td>
        </tr>
      </table>
    </div>
  `,
  styleUrl: '../sql-audit-page.scss',
})
export class AccessTable {
  protected readonly validation = inject(SqlValidation);

  readonly items = input.required<readonly SqlAccessItem[]>();
  readonly columns = input<readonly AccessColumn[]>(PERMISSION_COLUMNS);
  /** Accessible name of the table. */
  readonly tableLabel = input.required<string>();
  readonly emptyText = input.required<string>();
  readonly canValidate = input.required<boolean>();
  readonly sortable = input(false);
  readonly sortActive = input('');
  readonly sortDirection = input<SortDirection>('');

  readonly toggled = output<AccessToggle>();
  readonly sortChange = output<Sort>();

  protected readonly columnList = computed(() => [...this.columns()]);

  protected readonly label = sqlLabel;
  protected readonly accountName = accountName;
  protected readonly levelLabel = levelLabel;
  protected readonly securablePath = securablePath;
  protected readonly securableKind = securableKind;
  protected readonly accessElement = accessElement;
  protected readonly currentStatus = currentStatus;
  protected readonly validationStatus = validationStatus;
  protected readonly staleStatus = STALE_STATUS;
}
