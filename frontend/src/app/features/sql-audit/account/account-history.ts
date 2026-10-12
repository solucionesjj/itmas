import { DatePipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Subject, catchError, combineLatest, of, startWith, switchMap, tap } from 'rxjs';
import { SqlStatus } from '../components/sql-status';
import { SqlAuditApi } from '../data/sql-audit-api';
import { changeValue, historyElement, sqlLabel } from '../data/sql-audit-labels';
import { Paged, SqlAccountHistoryEntry } from '../data/sql-audit.models';

/**
 * Change log of an account and its elements, together with the validation history, newest first (SPEC-0005 RF-22).
 * `version` changes when the page saves a validation, so the new entries show up.
 */
@Component({
  selector: 'app-account-history',
  imports: [
    DatePipe,
    MatButtonModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
    SqlStatus,
  ],
  template: `
    @if (loading()) {
      <mat-progress-bar mode="indeterminate" aria-label="Cargando el registro de cambios" />
    }
    @if (failed()) {
      <div class="error-state" role="alert">
        <app-sql-status
          [status]="{
            label: 'No se pudo cargar el registro de cambios.',
            icon: 'error',
            tone: 'error',
          }"
        />
        <button mat-stroked-button (click)="retry()">
          <mat-icon>refresh</mat-icon>
          Reintentar
        </button>
      </div>
    } @else {
      <div class="table-container">
        <table
          mat-table
          [dataSource]="result()?.items ?? []"
          aria-label="Registro de cambios y validaciones"
        >
          <ng-container matColumnDef="at">
            <th mat-header-cell *matHeaderCellDef>Fecha</th>
            <td mat-cell *matCellDef="let entry">{{ entry.at | date: 'medium' }}</td>
          </ng-container>
          <ng-container matColumnDef="type">
            <th mat-header-cell *matHeaderCellDef>Cambio</th>
            <td mat-cell *matCellDef="let entry">{{ label('changeType', entry.type) }}</td>
          </ng-container>
          <ng-container matColumnDef="element">
            <th mat-header-cell *matHeaderCellDef>Elemento</th>
            <td mat-cell *matCellDef="let entry">
              {{ historyElement(entry) }}
              @for (change of entry.changes; track $index) {
                <span class="secondary">
                  {{ label('changeField', change.field) }}:
                  {{ changeValue(change.field, change.before) }} →
                  {{ changeValue(change.field, change.after) }}
                </span>
              }
            </td>
          </ng-container>
          <ng-container matColumnDef="by">
            <th mat-header-cell *matHeaderCellDef>Persona</th>
            <td mat-cell *matCellDef="let entry">
              {{ entry.by ?? 'Detectado en la recolección' }}
            </td>
          </ng-container>
          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
          <tr class="mat-mdc-no-data-row" *matNoDataRow>
            <td class="empty" [attr.colspan]="columns.length">
              {{ loading() ? 'Cargando…' : 'Sin cambios registrados.' }}
            </td>
          </tr>
        </table>
      </div>
      <mat-paginator
        [length]="result()?.total ?? 0"
        [pageIndex]="pageIndex()"
        [pageSize]="pageSize()"
        [pageSizeOptions]="[10, 20, 50, 100]"
        (page)="onPage($event)"
        aria-label="Paginación del registro de cambios"
      />
    }
  `,
  styleUrl: '../sql-audit-page.scss',
})
export class AccountHistory {
  private readonly api = inject(SqlAuditApi);

  readonly accountId = input.required<string>();
  readonly version = input(0);

  protected readonly columns = ['at', 'type', 'element', 'by'];
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(10);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly result = signal<Paged<SqlAccountHistoryEntry> | null>(null);
  private readonly reload = new Subject<void>();

  protected readonly label = sqlLabel;
  protected readonly historyElement = historyElement;
  protected readonly changeValue = changeValue;

  constructor() {
    combineLatest([
      toObservable(this.accountId),
      toObservable(this.version),
      toObservable(this.pageIndex),
      toObservable(this.pageSize),
      this.reload.pipe(startWith(undefined)),
    ])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
        }),
        switchMap(([id, , pageIndex, pageSize]) =>
          this.api.getAccountChanges(id, pageIndex + 1, pageSize).pipe(
            catchError(() => {
              this.failed.set(true);
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
      });
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  protected retry(): void {
    this.reload.next();
  }
}
