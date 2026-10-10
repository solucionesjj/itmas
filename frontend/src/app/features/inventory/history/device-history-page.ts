import { DatePipe } from '@angular/common';
import { Component, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { Subject, catchError, combineLatest, of, startWith, switchMap, tap } from 'rxjs';
import { InventoryApi } from '../data/inventory-api';
import { countryName } from '../data/inventory-labels';
import { DeviceHeader, DeviceReportListItem, Paged } from '../data/inventory.models';
import { DeviceHeaderComponent } from './device-header';

/** History of the records sent by a device, newest first (SPEC-0003 RF-15, RF-16). */
@Component({
  selector: 'app-device-history-page',
  imports: [
    DatePipe,
    DeviceHeaderComponent,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
    RouterLink,
  ],
  templateUrl: './device-history-page.html',
  styleUrl: '../inventory-page.scss',
})
export class DeviceHistoryPage {
  private readonly api = inject(InventoryApi);
  private readonly router = inject(Router);

  readonly id = input.required<string>();

  protected readonly columns = [
    'collectedAt',
    'receivedAt',
    'publicIp',
    'connectionIp',
    'agentVersion',
    'unavailable',
  ];
  protected readonly device = signal<DeviceHeader | null>(null);
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);
  protected readonly result = signal<Paged<DeviceReportListItem> | null>(null);
  private readonly reload = new Subject<void>();

  constructor() {
    toObservable(this.id)
      .pipe(
        switchMap((id) => this.api.getDevice(id).pipe(catchError(() => of(null)))),
        takeUntilDestroyed(),
      )
      .subscribe((versioned) => this.device.set(versioned?.device ?? null));

    combineLatest([
      toObservable(this.id),
      toObservable(this.pageIndex),
      toObservable(this.pageSize),
      this.reload.pipe(startWith(undefined)),
    ])
      .pipe(
        tap(() => {
          this.loading.set(true);
          this.failed.set(false);
        }),
        switchMap(([id, pageIndex, pageSize]) =>
          this.api.getReports(id, pageIndex + 1, pageSize).pipe(
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

  protected location(report: DeviceReportListItem): string {
    return [report.city, report.region, countryName(report.countryCode)].filter(Boolean).join(', ');
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  protected retry(): void {
    this.reload.next();
  }

  protected open(report: DeviceReportListItem): void {
    void this.router.navigate(['/inventory', this.id(), 'reports', report.id]);
  }
}
