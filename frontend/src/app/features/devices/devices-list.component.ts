import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DevicesService } from './devices.service';
import { Device, DeviceCategory } from './device.model';

@Component({
  selector: 'app-devices-list',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    DatePipe,
    MatFormFieldModule,
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
  private readonly fb = inject(FormBuilder);

  protected readonly displayedColumns = ['hostname', 'category', 'os', 'lastSeen'];
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
}
