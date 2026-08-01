import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../core/services/auth.service';
import { ReportsService } from './reports.service';
import { ReportFormat, ReportType } from './report.model';
import { AlertStatus } from '../alerts/alert.model';
import { DeviceCategory } from '../devices/device.model';

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatRadioModule,
    MatSelectModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './reports.component.html',
  styleUrl: './reports.component.scss'
})
export class ReportsComponent {
  private readonly fb = inject(FormBuilder);
  private readonly reportsService = inject(ReportsService);
  private readonly authService = inject(AuthService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly canExportAlerts = computed(() => {
    const role = this.authService.currentUser()?.role;
    return role === 'administrator' || role === 'auditor';
  });

  protected readonly downloading = signal(false);

  readonly form = this.fb.nonNullable.group({
    type: 'devices' as ReportType,
    format: 'csv' as ReportFormat,
    category: '' as '' | DeviceCategory,
    osName: '',
    hostname: '',
    status: '' as '' | AlertStatus,
    from: '',
    to: ''
  });

  protected download(): void {
    if (this.downloading()) {
      return;
    }

    const raw = this.form.getRawValue();
    // Usuario never sees the "Alertas" radio, but guard here too in case the
    // form value is somehow left over from a role change — the backend is
    // the real authority and will 403 regardless.
    const type: ReportType = raw.type === 'alerts' && !this.canExportAlerts() ? 'devices' : raw.type;

    this.downloading.set(true);
    this.reportsService
      .download({
        type,
        format: raw.format,
        category: raw.category || undefined,
        osName: raw.osName || undefined,
        hostname: raw.hostname || undefined,
        status: raw.status || undefined,
        from: raw.from || undefined,
        to: raw.to || undefined
      })
      .subscribe({
        next: (response) => this.triggerDownload(response.body, this.filenameFrom(response, type, raw.format)),
        error: (err) => {
          this.downloading.set(false);
          const message = err?.error?.error?.message ?? 'No se pudo generar el reporte.';
          this.snackBar.open(message, 'Cerrar', { duration: 4000 });
        },
        complete: () => this.downloading.set(false)
      });
  }

  private filenameFrom(
    response: { headers: { get(name: string): string | null } },
    type: ReportType,
    format: ReportFormat
  ): string {
    const disposition = response.headers.get('content-disposition');
    const match = disposition?.match(/filename="?([^";]+)"?/i);
    return match?.[1] ?? `${type === 'devices' ? 'equipos' : 'alertas'}-report.${format}`;
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
