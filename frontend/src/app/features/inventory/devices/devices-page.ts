import { DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import {
  EMPTY,
  Subject,
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  filter,
  map,
  of,
  startWith,
  switchMap,
  tap,
} from 'rxjs';
import { IfPermission } from '../../../auth/directives/if-permission';
import { AuthService } from '../../../auth/services/auth-service';
import { problemMessage } from '../../../core/models/problem-message';
import { NotificationService } from '../../../core/services/notification-service';
import { confirm } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { InventoryApi } from '../data/inventory-api';
import { ownerLabel, statusLabel, typeLabel } from '../data/inventory-labels';
import {
  DeviceCreated,
  DeviceListItem,
  DeviceSort,
  DeviceStatusFilter,
  DeviceType,
  Paged,
} from '../data/inventory.models';
import {
  DeviceFormDialog,
  DeviceFormDialogData,
  DeviceFormDialogResult,
} from './device-form-dialog';
import { showEnrollmentCode } from './enrollment-code-dialog';

/** Device list (SPEC-0003 RF-12..RF-14): one row per device with its owner, last record and history size. */
@Component({
  selector: 'app-devices-page',
  imports: [
    DatePipe,
    IfPermission,
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatMenuModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatSortModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './devices-page.html',
  styleUrl: '../inventory-page.scss',
})
export class DevicesPage {
  private readonly api = inject(InventoryApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  protected readonly auth = inject(AuthService);

  protected readonly columns = [
    'name',
    'type',
    'owner',
    'lastCollectedAt',
    'reportCount',
    'status',
    'actions',
  ];
  protected readonly search = signal('');
  protected readonly status = signal<DeviceStatusFilter>('current');
  protected readonly type = signal<DeviceType | ''>('');
  protected readonly sort = signal<{ active: DeviceSort; descending: boolean }>({
    active: 'name',
    descending: false,
  });
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly result = signal<Paged<DeviceListItem> | null>(null);
  private readonly reload = new Subject<void>();

  protected readonly typeLabel = typeLabel;
  protected readonly statusLabel = statusLabel;
  protected readonly ownerLabel = ownerLabel;

  /** Filters at their defaults: an empty result then means there are no devices at all (§8.3). */
  protected readonly unfiltered = computed(
    () => this.search() === '' && this.status() === 'current' && this.type() === '',
  );
  protected readonly canManage = computed(
    () => this.auth.hasPermission('devices.update') || this.auth.hasPermission('devices.delete'),
  );
  protected readonly canReadReports = computed(() =>
    this.auth.hasPermission('devices.reports.read'),
  );

  constructor() {
    combineLatest([
      toObservable(this.search).pipe(debounceTime(300), distinctUntilChanged()),
      toObservable(this.status),
      toObservable(this.type),
      toObservable(this.sort),
      toObservable(this.pageIndex),
      toObservable(this.pageSize),
      this.reload.pipe(startWith(undefined)),
    ])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
        }),
        switchMap(([search, status, type, sort, pageIndex, pageSize]) =>
          this.api
            .searchDevices({
              search,
              status,
              type,
              sort: sort.active,
              descending: sort.descending,
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

  protected onSearch(value: string): void {
    this.pageIndex.set(0);
    this.search.set(value.trim());
  }

  protected onStatus(value: DeviceStatusFilter): void {
    this.pageIndex.set(0);
    this.status.set(value);
  }

  protected onType(value: DeviceType | ''): void {
    this.pageIndex.set(0);
    this.type.set(value);
  }

  protected onSort(event: Sort): void {
    const active = (event.direction ? event.active : 'name') as DeviceSort;
    this.pageIndex.set(0);
    this.sort.set({ active, descending: event.direction === 'desc' });
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  protected retry(): void {
    this.reload.next();
  }

  /** Secondary text under the name: the hostname reported by the agent, when it differs (RF-12). */
  protected hostnameHint(device: DeviceListItem): string | null {
    return device.hostname && device.hostname.toLowerCase() !== device.name.toLowerCase()
      ? device.hostname
      : null;
  }

  protected openHistory(device: DeviceListItem): void {
    void this.router.navigate(['/inventory', device.id, 'reports']);
  }

  protected openLatest(device: DeviceListItem): void {
    void this.router.navigate(['/inventory', device.id, 'reports', 'latest']);
  }

  protected create(): void {
    this.openForm({ mode: 'create' })
      .pipe(filter((result): result is DeviceCreated => !!result && 'enrollmentCode' in result))
      .subscribe((created) => {
        this.reload.next();
        showEnrollmentCode(this.dialog, { deviceName: created.name, code: created.enrollmentCode });
      });
  }

  protected edit(device: DeviceListItem): void {
    this.api
      .getDevice(device.id)
      .pipe(
        catchError((error: unknown) => {
          this.notifications.error(problemMessage(error, 'No se pudo cargar el equipo.'));
          return EMPTY;
        }),
        switchMap(({ device: detail, etag }) =>
          this.openForm({ mode: 'edit', device: detail, etag }),
        ),
        filter((result) => !!result),
      )
      .subscribe(() => {
        this.notifications.info('Equipo actualizado.');
        this.reload.next();
      });
  }

  protected regenerate(device: DeviceListItem): void {
    confirm(this.dialog, {
      title: 'Regenerar código',
      message:
        'El código anterior dejará de servir. Si el agente ya está instalado, seguirá funcionando hasta que se reinstale.',
      confirmLabel: 'Regenerar',
    })
      .pipe(
        filter((confirmed) => confirmed === true),
        switchMap(() =>
          this.api.regenerateEnrollmentCode(device.id).pipe(
            catchError((error: unknown) => {
              this.notifications.error(problemMessage(error));
              return EMPTY;
            }),
          ),
        ),
        switchMap((issued) =>
          showEnrollmentCode(this.dialog, { deviceName: device.name, code: issued.enrollmentCode }),
        ),
      )
      .subscribe(() => this.reload.next());
  }

  protected delete(device: DeviceListItem): void {
    confirm(this.dialog, {
      title: `Eliminar ${device.name}`,
      message: 'El agente del equipo dejará de enviar datos. Esta acción no se puede deshacer.',
      confirmLabel: 'Eliminar',
    })
      .pipe(
        filter((confirmed) => confirmed === true),
        switchMap(() =>
          this.api.deleteDevice(device.id).pipe(
            map(() => true),
            catchError((error: unknown) => {
              this.notifications.error(problemMessage(error));
              return of(false);
            }),
          ),
        ),
        filter((deleted) => deleted),
      )
      .subscribe(() => {
        this.notifications.info('Equipo eliminado.');
        this.reload.next();
      });
  }

  private openForm(data: DeviceFormDialogData) {
    return this.dialog
      .open<DeviceFormDialog, DeviceFormDialogData, DeviceFormDialogResult>(DeviceFormDialog, {
        data,
      })
      .afterClosed();
  }
}
