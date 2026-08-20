import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { forkJoin } from 'rxjs';
import { ViewError, toViewError } from '../../core/utils/api-error.util';
import { DashboardService } from './dashboard.service';
import { DeviceStats, OsStat } from './stats.model';
import { OsDistributionChartComponent } from './os-distribution-chart.component';

interface Kpi {
  readonly label: string;
  readonly value: number;
}

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    DecimalPipe,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    OsDistributionChartComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  private readonly dashboardService = inject(DashboardService);

  protected readonly deviceStats = signal<DeviceStats | null>(null);
  protected readonly osStats = signal<OsStat[]>([]);
  protected readonly error = signal<ViewError | null>(null);

  // §10.4: skeletons on first load, a 2px bar over stale figures on a refresh.
  protected readonly loading = signal(false);
  protected readonly firstLoad = signal(true);
  protected readonly showSkeletons = computed(() => this.loading() && this.firstLoad());
  protected readonly refreshing = computed(() => this.loading() && !this.firstLoad());

  /**
   * §10.2: one number, one label, at most one comparison — and no comparison here,
   * because `GET /stats/devices` carries only current counts. A delta would have to
   * be invented, and §10.2 requires it to state its comparison period, so the tiles
   * deliberately have none until the API can supply one. `--delta-up`/`--delta-down`
   * therefore stay unused, like §2.6's severity levels. See BL-029.
   */
  protected readonly kpis = computed<Kpi[]>(() => {
    const stats = this.deviceStats();
    return [
      { label: 'Equipos totales', value: stats?.total ?? 0 },
      { label: 'Equipos de colaboradores', value: stats?.collaborator ?? 0 },
      { label: 'Equipos de infraestructura', value: stats?.infrastructure ?? 0 }
    ];
  });

  protected readonly hasData = computed(
    () => this.deviceStats() !== null && (this.deviceStats()?.total ?? 0) > 0
  );

  constructor() {
    this.reload();
  }

  protected reload(): void {
    this.loading.set(true);
    this.error.set(null);

    // Both figures belong to one view, so one failure is one error state rather
    // than a half-rendered dashboard.
    forkJoin({
      devices: this.dashboardService.getDeviceStats(),
      os: this.dashboardService.getOsStats()
    }).subscribe({
      next: ({ devices, os }) => {
        this.deviceStats.set(devices);
        this.osStats.set(os);
        this.loading.set(false);
        this.firstLoad.set(false);
      },
      error: (err) => {
        this.error.set(toViewError(err, 'No se pudieron cargar las estadísticas.'));
        this.loading.set(false);
        this.firstLoad.set(false);
      }
    });
  }
}
