import { Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Subject, catchError, combineLatest, of, startWith, switchMap, tap } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import {
  AccessTable,
  AccessToggle,
  PERMISSION_COLUMNS,
  ROLE_COLUMNS,
} from '../components/access-table';
import { SqlStatus } from '../components/sql-status';
import { SqlAuditApi } from '../data/sql-audit-api';
import { options } from '../data/sql-audit-labels';
import {
  AccessKind,
  ItemValidationFilter,
  Paged,
  RiskLevel,
  SqlAccessItem,
} from '../data/sql-audit.models';
import { SqlValidation, ValidationOutcome } from '../data/sql-validation';

/**
 * Role memberships or permissions of an account (SPEC-0005 RF-22). Permissions can be filtered by risk, validation,
 * database and object type; the API orders them by risk and path.
 */
@Component({
  selector: 'app-account-items',
  imports: [
    AccessTable,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    SqlStatus,
  ],
  template: `
    @if (kind() === 'permission') {
      <div class="filters">
        <mat-form-field appearance="outline">
          <mat-label>Riesgo</mat-label>
          <mat-select [value]="risk()" (valueChange)="setFilter(risk, $event)">
            <mat-option value="">Todos</mat-option>
            <mat-option value="critical">Crítico</mat-option>
            <mat-option value="high">Alto</mat-option>
            <mat-option value="general">General</mat-option>
            <mat-option value="denied">Denegado</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>Validación</mat-label>
          <mat-select
            [value]="validationFilter()"
            (valueChange)="setFilter(validationFilter, $event)"
          >
            <mat-option value="">Todos los actuales</mat-option>
            <mat-option value="pending">Pendientes</mat-option>
            <mat-option value="valid">Válidos</mat-option>
            <mat-option value="removed">Eliminados del motor</mat-option>
          </mat-select>
        </mat-form-field>
        @if (databases().length > 0) {
          <mat-form-field appearance="outline">
            <mat-label>Base de datos</mat-label>
            <mat-select [value]="database()" (valueChange)="setFilter(database, $event)">
              <mat-option value="">Todas</mat-option>
              @for (name of databases(); track name) {
                <mat-option [value]="name">{{ name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        }
        <mat-form-field appearance="outline">
          <mat-label>Tipo de objeto</mat-label>
          <mat-select [value]="objectType()" (valueChange)="setFilter(objectType, $event)">
            <mat-option value="">Todos</mat-option>
            @for (option of objectTypeOptions(); track option.value) {
              <mat-option [value]="option.value">{{ option.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>
    }

    @if (loading()) {
      <mat-progress-bar
        mode="indeterminate"
        [attr.aria-label]="kind() === 'permission' ? 'Cargando permisos' : 'Cargando roles'"
      />
    }

    @if (failed()) {
      <div class="error-state" role="alert">
        <app-sql-status
          [status]="{
            label:
              kind() === 'permission'
                ? 'No se pudieron cargar los permisos.'
                : 'No se pudieron cargar los roles.',
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
      <app-access-table
        [tableLabel]="kind() === 'permission' ? 'Permisos de la cuenta' : 'Roles de la cuenta'"
        [items]="result()?.items ?? []"
        [columns]="kind() === 'permission' ? permissionColumns : roleColumns"
        [emptyText]="emptyText()"
        [canValidate]="canValidate()"
        (toggled)="toggle($event)"
      />
      @if ((result()?.total ?? 0) > pageSizeOptions[0] || pageIndex() > 0) {
        <mat-paginator
          [length]="result()?.total ?? 0"
          [pageIndex]="pageIndex()"
          [pageSize]="pageSize()"
          [pageSizeOptions]="pageSizeOptions"
          (page)="onPage($event)"
          [attr.aria-label]="
            kind() === 'permission' ? 'Paginación de permisos' : 'Paginación de roles'
          "
        />
      }
    }
  `,
  styleUrl: '../sql-audit-page.scss',
})
export class AccountItems {
  private readonly api = inject(SqlAuditApi);
  private readonly validation = inject(SqlValidation);
  private readonly auth = inject(AuthService);

  readonly accountId = input.required<string>();
  readonly kind = input.required<AccessKind>();
  /** Databases and object types present in the account's permissions, for the filters. */
  readonly databases = input<readonly string[]>([]);
  readonly objectTypes = input<readonly string[]>([]);

  /** Change of the account's pending counter. */
  readonly pendingDelta = output<number>();
  /** A validation request settled: `outdated` means the element changed or was removed. */
  readonly settled = output<ValidationOutcome>();

  protected readonly permissionColumns = PERMISSION_COLUMNS;
  protected readonly roleColumns = ROLE_COLUMNS;
  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly risk = signal<RiskLevel | ''>('');
  protected readonly validationFilter = signal<ItemValidationFilter>('');
  protected readonly database = signal('');
  protected readonly objectType = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly result = signal<Paged<SqlAccessItem> | null>(null);
  private readonly reload = new Subject<void>();

  protected readonly objectTypeOptions = computed(() => options('objectType', this.objectTypes()));
  protected readonly canValidate = computed(() =>
    this.auth.hasPermission('sql-audit.accesses.validate'),
  );
  protected readonly emptyText = computed(() => {
    if (this.loading()) {
      return 'Cargando…';
    }
    const otherFilters = this.risk() !== '' || this.database() !== '' || this.objectType() !== '';
    if (this.validationFilter() === 'pending' && !otherFilters) {
      return 'No hay elementos pendientes de validar.';
    }
    if (this.validationFilter() !== '' || otherFilters) {
      return 'Ningún permiso coincide con los filtros.';
    }
    return this.kind() === 'permission'
      ? 'La cuenta no tiene permisos propios.'
      : 'La cuenta no es miembro de ningún rol.';
  });

  constructor() {
    combineLatest([
      toObservable(this.accountId),
      toObservable(this.kind),
      toObservable(this.risk),
      toObservable(this.validationFilter),
      toObservable(this.database),
      toObservable(this.objectType),
      toObservable(this.pageIndex),
      toObservable(this.pageSize),
      this.reload.pipe(startWith(undefined)),
    ])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
        }),
        switchMap(([id, kind, risk, validation, database, objectType, pageIndex, pageSize]) =>
          this.api
            .getAccountItems(id, {
              kind,
              risk,
              validation,
              database,
              objectType,
              page: pageIndex + 1,
              pageSize,
            })
            .pipe(
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

  /** Sets a filter and goes back to the first page. */
  protected setFilter<T>(target: { set(value: T): void }, value: T): void {
    this.pageIndex.set(0);
    target.set(value);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  protected retry(): void {
    this.reload.next();
  }

  protected toggle({ item, valid }: AccessToggle): void {
    this.validation
      .toggleItem(this.result, item, valid, (delta) => this.pendingDelta.emit(delta))
      .subscribe((outcome) => {
        if (outcome === 'outdated') {
          this.reload.next();
        }
        this.settled.emit(outcome);
      });
  }
}
