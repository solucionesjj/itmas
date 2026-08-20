import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Observable, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/services/auth.service';
import { ViewError, toViewError } from '../../core/utils/api-error.util';
import { SecurityGroupRulesService } from './security-group-rules.service';
import { SecurityGroupSyncService } from './security-group-sync.service';
import {
  DistinctGroup,
  ExportFormat,
  SecurityGroupRule,
  SecurityGroupRuleSortField,
  SecurityGroupRuleStatus,
  SortDirection
} from './security-group-rule.model';
import {
  ObservationDialogComponent,
  ObservationDialogData,
  ObservationDialogResult
} from './observation-dialog.component';
import { StatusChipComponent } from './status-chip.component';

const DEFAULT_SORT: SecurityGroupRuleSortField = 'securityGroupName';

@Component({
  selector: 'app-security-group-rules-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatIconModule,
    MatProgressBarModule,
    MatProgressSpinnerModule,
    StatusChipComponent
  ],
  templateUrl: './security-group-rules-list.component.html',
  styleUrl: './security-group-rules-list.component.scss'
})
export class SecurityGroupRulesListComponent {
  private readonly rulesService = inject(SecurityGroupRulesService);
  private readonly syncService = inject(SecurityGroupSyncService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly displayedColumns = [
    'securityGroupName',
    'securityGroupId',
    'attachedResources',
    'ruleName',
    'ruleId',
    'source',
    'destination',
    'protocol',
    'portRange',
    'status',
    'createdAt',
    'reviewedAt',
    'authorizedAt',
    'actions'
  ];

  protected readonly items = signal<SecurityGroupRule[]>([]);
  protected readonly total = signal(0);
  protected readonly error = signal<ViewError | null>(null);

  // §10.4: skeletons on the first load, a 2px bar over stale rows on a refresh.
  protected readonly loading = signal(false);
  protected readonly firstLoad = signal(true);
  protected readonly showSkeletons = computed(() => this.loading() && this.firstLoad());
  protected readonly refreshing = computed(() => this.loading() && !this.firstLoad());

  /** Drives the two different empty messages §10.4 requires. */
  protected readonly filtersActive = signal(false);
  protected readonly syncing = signal(false);
  protected readonly groups = signal<DistinctGroup[]>([]);

  protected readonly sortBy = signal<SecurityGroupRuleSortField>(DEFAULT_SORT);
  protected readonly sortDir = signal<SortDirection>('asc');

  protected readonly isAuditor = computed(
    () => this.authService.currentUser()?.role === 'auditor'
  );
  protected readonly isAdministrador = computed(
    () => this.authService.currentUser()?.role === 'administrator'
  );
  protected readonly canSync = computed(
    () => this.isAdministrador() || this.isAuditor()
  );

  protected readonly filters = this.fb.nonNullable.group({
    q: '',
    securityGroupId: '' as string,
    status: '' as '' | SecurityGroupRuleStatus,
    createdFrom: '',
    createdTo: '',
    reviewedFrom: '',
    reviewedTo: '',
    authorizedFrom: '',
    authorizedTo: ''
  });

  private page = 0;
  private readonly pageSize = 20;

  constructor() {
    this.loadGroups();
    this.reload();

    this.filters.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe(() => {
        this.page = 0;
        this.reload();
      });
  }

  protected onPage(event: PageEvent): void {
    this.page = event.pageIndex;
    this.reload();
  }

  protected onSortChange(sort: Sort): void {
    if (!sort.direction) {
      this.sortBy.set(DEFAULT_SORT);
      this.sortDir.set('asc');
    } else {
      this.sortBy.set(sort.active as SecurityGroupRuleSortField);
      this.sortDir.set(sort.direction);
    }
    this.page = 0;
    this.reload();
  }

  protected attachedResourceLabel(rule: SecurityGroupRule): string {
    if (rule.attachedResources.length === 0) {
      return 'Sin recurso asociado';
    }
    const [first, ...rest] = rule.attachedResources;
    const label = first.resourceName ?? first.resourceId;
    return rest.length > 0 ? `${label} (+${rest.length})` : label;
  }

  protected review(rule: SecurityGroupRule): void {
    this.openObservationDialog(
      { title: 'Marcar como revisado', actionLabel: 'Marcar como revisado' },
      (observation) => this.rulesService.review(rule._id, observation)
    );
  }

  protected authorize(rule: SecurityGroupRule): void {
    this.openObservationDialog(
      {
        title: 'Marcar como autorizado',
        actionLabel: 'Marcar como autorizado'
      },
      (observation) => this.rulesService.authorize(rule._id, observation)
    );
  }

  protected sync(): void {
    if (this.syncing()) {
      return;
    }
    this.syncing.set(true);
    this.syncService.run().subscribe({
      next: (run) => {
        this.syncing.set(false);
        if (run.status === 'failure') {
          this.snackBar.open(
            'No se pudo conectar con AWS — verifica las credenciales configuradas en el backend.',
            'Cerrar',
            { duration: 6000 }
          );
          return;
        }
        const s = run.summary;
        const suffix =
          run.status === 'partial_failure'
            ? ` (${s.groupsFailed} grupo(s) con error — revisar logs del backend)`
            : '';
        this.snackBar.open(
          `Sincronización completa: ${s.groupsProcessed} grupos, ${s.rulesProcessed} reglas, ${s.rulesCreated} nuevas, ${s.rulesMarkedDeleted} eliminada(s).${suffix}`,
          'Cerrar',
          { duration: 5000 }
        );
        this.loadGroups();
        this.reload();
      },
      error: (err) => {
        this.syncing.set(false);
        const message =
          err.error?.error?.message ?? 'No se pudo ejecutar la sincronización.';
        this.snackBar.open(message, 'Cerrar', { duration: 4000 });
      }
    });
  }

  protected export(format: ExportFormat): void {
    const raw = this.filters.getRawValue();
    this.rulesService
      .export({
        format,
        q: raw.q || undefined,
        securityGroupId: raw.securityGroupId || undefined,
        status: raw.status || undefined,
        createdFrom: raw.createdFrom || undefined,
        createdTo: raw.createdTo || undefined,
        reviewedFrom: raw.reviewedFrom || undefined,
        reviewedTo: raw.reviewedTo || undefined,
        authorizedFrom: raw.authorizedFrom || undefined,
        authorizedTo: raw.authorizedTo || undefined
      })
      .subscribe({
        next: (response) =>
          this.triggerDownload(response.body, this.filenameFrom(response, format)),
        error: (err) => {
          const message =
            err.error?.error?.message ?? 'No se pudo exportar el catálogo.';
          this.snackBar.open(message, 'Cerrar', { duration: 4000 });
        }
      });
  }

  private openObservationDialog(
    data: ObservationDialogData,
    action: (observation: string) => Observable<SecurityGroupRule>
  ): void {
    const ref = this.dialog.open<
      ObservationDialogComponent,
      ObservationDialogData,
      ObservationDialogResult
    >(ObservationDialogComponent, { data });

    ref.afterClosed().subscribe((result) => {
      if (!result) {
        return;
      }
      action(result.observation).subscribe({
        next: () => {
          this.snackBar.open('Registro actualizado.', 'Cerrar', {
            duration: 3000
          });
          this.reload();
        },
        error: (err) => {
          const message =
            err.error?.error?.message ?? 'No se pudo actualizar el registro.';
          this.snackBar.open(message, 'Cerrar', { duration: 4000 });
        }
      });
    });
  }

  private loadGroups(): void {
    this.rulesService.listGroups().subscribe((groups) => this.groups.set(groups));
  }

  protected clearFilters(): void {
    this.filters.reset({
      q: '',
      securityGroupId: '',
      status: '',
      createdFrom: '',
      createdTo: '',
      reviewedFrom: '',
      reviewedTo: '',
      authorizedFrom: '',
      authorizedTo: ''
    });
  }

  protected reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const raw = this.filters.getRawValue();
    // Any of the eight filters being set changes which empty message applies.
    this.filtersActive.set(Object.values(raw).some((value) => Boolean(value)));
    this.rulesService
      .list({
        q: raw.q || undefined,
        securityGroupId: raw.securityGroupId || undefined,
        status: raw.status || undefined,
        createdFrom: raw.createdFrom || undefined,
        createdTo: raw.createdTo || undefined,
        reviewedFrom: raw.reviewedFrom || undefined,
        reviewedTo: raw.reviewedTo || undefined,
        authorizedFrom: raw.authorizedFrom || undefined,
        authorizedTo: raw.authorizedTo || undefined,
        sortBy: this.sortBy(),
        sortDir: this.sortDir(),
        page: this.page + 1,
        limit: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.total.set(result.total);
          this.loading.set(false);
          this.firstLoad.set(false);
        },
        error: (err) => {
          this.error.set(toViewError(err, 'No se pudieron cargar las reglas de firewall.'));
          this.loading.set(false);
          this.firstLoad.set(false);
        }
      });
  }

  private filenameFrom(
    response: { headers: { get(name: string): string | null } },
    format: ExportFormat
  ): string {
    const disposition = response.headers.get('content-disposition');
    const match = disposition?.match(/filename="?([^";]+)"?/i);
    return match?.[1] ?? `security-group-rules-report.${format}`;
  }

  private triggerDownload(blob: Blob | null, filename: string): void {
    if (!blob) {
      return;
    }
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = filename;
    anchor.click();
    URL.revokeObjectURL(url);
  }
}
