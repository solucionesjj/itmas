import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { Subject, catchError, combineLatest, of, startWith, switchMap, tap } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { BytesPipe } from '../../../shared/pipes/bytes-pipe';
import { InventoryApi } from '../data/inventory-api';
import { LabelKind, countryName, label } from '../data/inventory-labels';
import { DeviceReport, Volume } from '../data/inventory.models';
import { DeviceHeaderComponent } from '../history/device-header';

interface ReportSection {
  readonly id: string;
  readonly title: string;
}

/**
 * One record of a device shown as a machine report grouped by sections (SPEC-0003 RF-17, RF-18): the latest one, or a
 * record of the history. Every value comes from the device: it is rendered as text only (RNF-4).
 */
@Component({
  selector: 'app-device-report-page',
  imports: [
    BytesPipe,
    DatePipe,
    DecimalPipe,
    DeviceHeaderComponent,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    RouterLink,
  ],
  templateUrl: './device-report-page.html',
  styleUrls: ['../inventory-page.scss', './device-report-page.scss'],
})
export class DeviceReportPage {
  private readonly api = inject(InventoryApi);

  readonly id = input.required<string>();
  /** Absent on the "latest" route. */
  readonly reportId = input<string | undefined>(undefined);

  protected readonly report = signal<DeviceReport | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly failed = signal(false);
  protected readonly softwareFilter = signal('');
  private readonly reload = new Subject<void>();

  protected readonly label = (kind: LabelKind, value: string | null | undefined) =>
    label(kind, value);
  protected readonly countryName = countryName;

  protected readonly sections: readonly ReportSection[] = [
    { id: 'system', title: 'Características del equipo' },
    { id: 'security', title: 'Seguridad' },
    { id: 'network', title: 'Red' },
    { id: 'storage', title: 'Almacenamiento' },
    { id: 'accounts', title: 'Usuarios locales' },
    { id: 'software', title: 'Software instalado' },
  ];

  protected readonly diskColumns = ['model', 'size', 'mediaType', 'busType'];
  protected readonly volumeColumns = [
    'mountPoint',
    'fileSystem',
    'size',
    'usage',
    'disk',
    'encryption',
  ];
  protected readonly interfaceColumns = ['name', 'ipv4', 'ipv6'];
  protected readonly accountColumns = ['name', 'enabled', 'isAdmin', 'groups'];

  protected readonly inventory = computed(() => this.report()?.inventory ?? null);

  /** Reason of each section the agent could not collect (SPEC-0002 RF-9). */
  private readonly unavailable = computed(
    () => new Map((this.inventory()?.unavailable ?? []).map((item) => [item.section, item.reason])),
  );

  protected readonly systemVolume = computed(
    () => this.inventory()?.hardware.volumes?.find((volume) => volume.isSystem) ?? null,
  );

  /** Declared public IP and server-observed IP differ: proxy, VPN or tampered data (SPEC-0002 RF-16). */
  protected readonly ipMismatch = computed(() => {
    const report = this.report();
    const declared = report?.inventory.network.publicNetwork?.ip;
    return !!report?.connectionIp && !!declared && declared !== report.connectionIp;
  });

  protected readonly applications = computed(() => {
    const all = [...(this.inventory()?.software.applications ?? [])].sort((a, b) =>
      a.localeCompare(b, 'es', { sensitivity: 'base' }),
    );
    const text = this.softwareFilter().trim().toLocaleLowerCase('es');
    return text ? all.filter((name) => name.toLocaleLowerCase('es').includes(text)) : all;
  });

  constructor() {
    combineLatest([
      toObservable(this.id),
      toObservable(this.reportId),
      this.reload.pipe(startWith(undefined)),
    ])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
          this.notFound.set(false);
        }),
        switchMap(([id, reportId]) =>
          this.api.getReport(id, reportId).pipe(
            catchError((error: unknown) => {
              if (error instanceof ApiError && error.status === 404) {
                this.notFound.set(true);
              } else {
                this.failed.set(true);
              }
              return of(null);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((report) => {
        this.report.set(report);
        this.softwareFilter.set('');
        this.loading.set(false);
      });
  }

  /** "No disponible (motivo)" for a section the agent could not collect, or null when it is available. */
  protected unavailableText(section: string): string | null {
    const reason = this.unavailable().get(section);
    return reason ? `No disponible (${label('reason', reason).toLocaleLowerCase('es')})` : null;
  }

  protected usedPercent(volume: Volume): number {
    return volume.sizeBytes > 0
      ? Math.min(100, ((volume.sizeBytes - volume.freeBytes) / volume.sizeBytes) * 100)
      : 0;
  }

  protected encryption(volume: Volume): string {
    const status = label('encryption', volume.encryption.status);
    return volume.encryption.method
      ? `${status} (${label('encryptionMethod', volume.encryption.method)})`
      : status;
  }

  protected location(): string {
    const network = this.inventory()?.network.publicNetwork;
    return network
      ? [network.city, network.region, countryName(network.country)].filter(Boolean).join(', ')
      : '';
  }

  protected scrollTo(id: string): void {
    document
      .getElementById(`section-${id}`)
      ?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  protected retry(): void {
    this.reload.next();
  }
}
