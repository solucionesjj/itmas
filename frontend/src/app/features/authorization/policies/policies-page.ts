import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import {
  catchError,
  combineLatest,
  debounceTime,
  distinctUntilChanged,
  of,
  switchMap,
  tap,
} from 'rxjs';
import { IfPermission } from '../../../auth/directives/if-permission';
import { AuthorizationApi } from '../data/authorization-api';
import { Paged, PolicyStatusFilter, PolicySummary } from '../data/authorization.models';

@Component({
  selector: 'app-policies-page',
  imports: [
    DatePipe,
    IfPermission,
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    RouterLink,
  ],
  templateUrl: './policies-page.html',
  styleUrl: '../admin-page.scss',
})
export class PoliciesPage {
  private readonly api = inject(AuthorizationApi);
  private readonly router = inject(Router);

  protected readonly columns = ['name', 'permissionCount', 'userCount', 'status', 'updatedAt'];
  protected readonly search = signal('');
  protected readonly status = signal<PolicyStatusFilter>('all');
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly result = signal<Paged<PolicySummary> | null>(null);

  constructor() {
    combineLatest([
      toObservable(this.search).pipe(debounceTime(300), distinctUntilChanged()),
      toObservable(this.status),
      toObservable(this.pageIndex),
      toObservable(this.pageSize),
    ])
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(([search, status, pageIndex, pageSize]) =>
          this.api
            .searchPolicies(search, status, pageIndex + 1, pageSize)
            .pipe(catchError(() => of(null))),
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

  protected onStatus(value: PolicyStatusFilter): void {
    this.pageIndex.set(0);
    this.status.set(value);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  protected open(policy: PolicySummary): void {
    void this.router.navigate(['/admin/policies', policy.id]);
  }
}
