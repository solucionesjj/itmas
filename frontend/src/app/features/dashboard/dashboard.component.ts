import { Component, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DashboardService } from './dashboard.service';
import { DeviceStats, OsStat } from './stats.model';
import { OsDistributionChartComponent } from './os-distribution-chart.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [MatCardModule, MatProgressSpinnerModule, OsDistributionChartComponent],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent {
  private readonly dashboardService = inject(DashboardService);

  protected readonly loading = signal(true);
  protected readonly deviceStats = signal<DeviceStats | null>(null);
  protected readonly osStats = signal<OsStat[]>([]);

  constructor() {
    this.dashboardService.getDeviceStats().subscribe((stats) => {
      this.deviceStats.set(stats);
      this.loading.set(false);
    });
    this.dashboardService.getOsStats().subscribe((stats) => this.osStats.set(stats));
  }
}
