import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ViewError, toViewError } from '../../core/utils/api-error.util';
import { AlertsService } from './alerts.service';
import { Alert, AlertStatus, AlertType } from './alert.model';

@Component({
  selector: 'app-alerts-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    MatPaginatorModule
  ],
  templateUrl: './alerts-list.component.html',
  styleUrl: './alerts-list.component.scss'
})
export class AlertsListComponent {
  private readonly alertsService = inject(AlertsService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly displayedColumns = [
    'type',
    'deviceId',
    'detail',
    'createdAt',
    'status',
    'actions'
  ];
  protected readonly alerts = signal<Alert[]>([]);
  protected readonly total = signal(0);
  protected readonly error = signal<ViewError | null>(null);

  /**
   * §10.4 distinguishes the first load from a refresh: the first shows skeletons,
   * a refresh keeps the stale rows on screen under a 2px indeterminate bar. One
   * flag rather than two loading signals, so they cannot contradict each other.
   */
  protected readonly loading = signal(false);
  protected readonly firstLoad = signal(true);

  protected readonly showSkeletons = computed(() => this.loading() && this.firstLoad());
  protected readonly refreshing = computed(() => this.loading() && !this.firstLoad());

  protected readonly filters = this.fb.nonNullable.group({
    type: '' as '' | AlertType,
    status: '' as '' | AlertStatus,
    from: '',
    to: ''
  });

  /** Drives the two different empty messages §10.4 requires. */
  protected readonly filtersActive = signal(false);

  private page = 0;
  private readonly pageSize = 20;

  constructor() {
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

  protected clearFilters(): void {
    this.filters.reset({ type: '', status: '', from: '', to: '' });
  }

  protected detailSummary(detail: Record<string, unknown>): string {
    return Object.entries(detail)
      .map(([key, value]) => `${key}: ${JSON.stringify(value)}`)
      .join(', ');
  }

  protected toggleStatus(alert: Alert): void {
    const next: AlertStatus = alert.status === 'open' ? 'reviewed' : 'open';
    this.alertsService.updateStatus(alert._id, next).subscribe({
      next: () => {
        this.snackBar.open('Estado de la alerta actualizado.', 'Cerrar', {
          duration: 3000
        });
        this.reload();
      },
      error: (err) => {
        this.snackBar.open(
          toViewError(err, 'No se pudo actualizar la alerta.').message,
          'Cerrar',
          { duration: 4000 }
        );
      }
    });
  }

  protected reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const raw = this.filters.getRawValue();
    this.filtersActive.set(Boolean(raw.type || raw.status || raw.from || raw.to));

    this.alertsService
      .list({
        type: raw.type || undefined,
        status: raw.status || undefined,
        from: raw.from || undefined,
        to: raw.to || undefined,
        page: this.page + 1,
        limit: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.alerts.set(result.items);
          this.total.set(result.total);
          this.loading.set(false);
          this.firstLoad.set(false);
        },
        error: (err) => {
          this.error.set(toViewError(err, 'No se pudieron cargar las alertas.'));
          this.loading.set(false);
          this.firstLoad.set(false);
        }
      });
  }
}
