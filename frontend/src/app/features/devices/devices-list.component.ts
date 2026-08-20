import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatChipsModule } from '@angular/material/chips';
import { ActivatedRoute, Router } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/services/auth.service';
import { ViewError, toViewError } from '../../core/utils/api-error.util';
import {
  AppliedFilter,
  FilterMetaMap,
  anyFilterActive,
  describeFilters,
  filtersFromParams,
  paramsFromFilters
} from '../../core/utils/filter-url.util';
import { DevicesService } from './devices.service';
import { CreateDeviceRequest, Device, DeviceCategory } from './device.model';
import { DeviceFormDialogComponent } from './device-form-dialog.component';
import {
  ApiKeyRevealDialogComponent,
  ApiKeyRevealDialogData
} from './api-key-reveal-dialog.component';
import {
  RotateKeyConfirmDialogComponent,
  RotateKeyConfirmDialogData
} from './rotate-key-confirm-dialog.component';

/** Chip labels for the applied-filter bar (§10.1) — never a raw enum key. */
const FILTER_META: FilterMetaMap = {
  category: {
    label: 'Categoría',
    format: (value) => (value === 'collaborator' ? 'Colaborador' : 'Infraestructura')
  },
  hostname: { label: 'Hostname' },
  osName: { label: 'Sistema operativo' }
};

@Component({
  selector: 'app-devices-list',
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
    MatChipsModule,
    MatTableModule,
    MatPaginatorModule
  ],
  templateUrl: './devices-list.component.html',
  styleUrl: './devices-list.component.scss'
})
export class DevicesListComponent {
  private readonly devicesService = inject(DevicesService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  // Hiding "Crear dispositivo"/"Rotar clave" from non-Administradores is UX
  // only — the backend's own @Roles(ADMINISTRATOR) on POST /devices and
  // POST /devices/:id/rotate-key is the real enforcement (agent.md defense
  // in depth). Same pattern as shell.component's "Usuarios" nav link.
  protected readonly isAdmin = this.authService.currentUser()?.role === 'administrator';

  protected readonly displayedColumns = this.isAdmin
    ? ['hostname', 'category', 'os', 'lastSeen', 'actions']
    : ['hostname', 'category', 'os', 'lastSeen'];
  protected readonly devices = signal<Device[]>([]);
  protected readonly total = signal(0);
  protected readonly error = signal<ViewError | null>(null);

  // §10.4: the first load shows skeletons; a refresh keeps the stale rows under
  // a 2px indeterminate bar.
  protected readonly loading = signal(false);
  protected readonly firstLoad = signal(true);
  protected readonly showSkeletons = computed(() => this.loading() && this.firstLoad());
  protected readonly refreshing = computed(() => this.loading() && !this.firstLoad());

  protected readonly filters = this.fb.nonNullable.group({
    category: '' as '' | DeviceCategory,
    hostname: '',
    osName: ''
  });

  /** Drives the two different empty messages §10.4 requires. */
  protected readonly filtersActive = signal(false);
  /** The active query, shown above the data as removable chips (§10.1). */
  protected readonly appliedFilters = signal<AppliedFilter[]>([]);

  private page = 0; // zero-based, MatPaginator convention
  private readonly pageSize = 20;

  constructor() {
    // The URL is the source of truth on entry, so a filtered view is linkable and
    // survives a refresh. Seeded without emitting, or it would trigger a second
    // reload before the first has run.
    this.filters.patchValue(
      filtersFromParams(this.route.snapshot.queryParams, Object.keys(this.filters.controls)),
      { emitEvent: false }
    );
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

  protected openCreateDialog(): void {
    const ref = this.dialog.open<
      DeviceFormDialogComponent,
      unknown,
      CreateDeviceRequest
    >(DeviceFormDialogComponent);

    ref.afterClosed().subscribe((request) => {
      if (!request) {
        return;
      }
      this.devicesService.create(request).subscribe({
        next: (created) => {
          this.reload();
          this.openApiKeyReveal({
            hostname: created.hostname,
            deviceId: created.deviceId,
            apiKey: created.apiKey
          });
        },
        error: (err) => this.showError(err)
      });
    });
  }

  protected openRotateKeyDialog(device: Device): void {
    const confirmRef = this.dialog.open<
      RotateKeyConfirmDialogComponent,
      RotateKeyConfirmDialogData,
      boolean
    >(RotateKeyConfirmDialogComponent, { data: { hostname: device.hostname } });

    confirmRef.afterClosed().subscribe((confirmed) => {
      if (!confirmed) {
        return;
      }
      this.devicesService.rotateKey(device._id).subscribe({
        next: (rotated) => {
          this.snackBar.open('Clave rotada correctamente.', 'Cerrar', {
            duration: 3000
          });
          this.openApiKeyReveal({
            hostname: device.hostname,
            deviceId: rotated.deviceId,
            apiKey: rotated.apiKey
          });
        },
        error: (err) => this.showError(err)
      });
    });
  }

  private openApiKeyReveal(data: ApiKeyRevealDialogData): void {
    this.dialog.open<ApiKeyRevealDialogComponent, ApiKeyRevealDialogData>(
      ApiKeyRevealDialogComponent,
      { data, disableClose: true }
    );
  }

  protected clearFilters(): void {
    this.filters.reset({ category: '', hostname: '', osName: '' });
  }

  protected removeFilter(key: string): void {
    this.filters.get(key)?.setValue('');
  }

  protected reload(): void {
    this.loading.set(true);
    this.error.set(null);
    const raw = this.filters.getRawValue();
    this.filtersActive.set(anyFilterActive(raw));
    this.appliedFilters.set(describeFilters(raw, FILTER_META));
    // replaceUrl so typing in a filter does not stack one history entry per
    // keystroke — the back button should leave the view, not undo a character.
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: paramsFromFilters(raw),
      replaceUrl: true
    });
    this.devicesService
      .list({
        category: raw.category || undefined,
        hostname: raw.hostname || undefined,
        osName: raw.osName || undefined,
        page: this.page + 1,
        limit: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.devices.set(result.items);
          this.total.set(result.total);
          this.loading.set(false);
          this.firstLoad.set(false);
        },
        error: (err) => {
          this.error.set(toViewError(err, 'No se pudieron cargar los equipos.'));
          this.loading.set(false);
          this.firstLoad.set(false);
        }
      });
  }

  private showError(err: unknown): void {
    this.snackBar.open(
      toViewError(err, 'No se pudo completar la operación.').message,
      'Cerrar',
      { duration: 4000 }
    );
  }
}
