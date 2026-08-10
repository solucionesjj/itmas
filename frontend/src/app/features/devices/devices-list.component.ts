import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../core/services/auth.service';
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
    MatSelectModule,
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
  protected readonly loading = signal(false);

  protected readonly filters = this.fb.nonNullable.group({
    category: '' as '' | DeviceCategory,
    hostname: '',
    osName: ''
  });

  private page = 0; // zero-based, MatPaginator convention
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

  private reload(): void {
    this.loading.set(true);
    const raw = this.filters.getRawValue();
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
        },
        error: () => this.loading.set(false)
      });
  }

  private showError(err: { error?: { error?: { message?: string } } }): void {
    const message = err.error?.error?.message ?? 'No se pudo completar la operación.';
    this.snackBar.open(message, 'Cerrar', { duration: 4000 });
  }
}
