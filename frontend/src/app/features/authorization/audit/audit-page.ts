import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { catchError, combineLatest, of, switchMap, tap } from 'rxjs';
import { AuthorizationApi } from '../data/authorization-api';
import {
  AUDIT_ACTION_LABELS,
  AUDIT_RESULT_LABELS,
  AuditEvent,
  AuditFilter,
  Paged,
} from '../data/authorization.models';
import { AuditEventDialog } from './audit-event-dialog';

/** Authorization audit trail (read-only). */
@Component({
  selector: 'app-audit-page',
  imports: [
    DatePipe,
    MatButtonModule,
    MatChipsModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
  ],
  providers: [provideNativeDateAdapter()],
  templateUrl: './audit-page.html',
  styleUrl: '../admin-page.scss',
})
export class AuditPage {
  private readonly api = inject(AuthorizationApi);
  private readonly dialog = inject(MatDialog);

  protected readonly columns = ['occurredAt', 'actor', 'action', 'resource', 'result'];
  protected readonly actions = Object.entries(AUDIT_ACTION_LABELS);
  protected readonly actionLabels = AUDIT_ACTION_LABELS;
  protected readonly resultLabels: Readonly<Record<string, string>> = AUDIT_RESULT_LABELS;

  protected readonly filter = signal<AuditFilter>({});
  protected readonly pageIndex = signal(0);
  protected readonly pageSize = signal(50);
  protected readonly loading = signal(true);
  protected readonly result = signal<Paged<AuditEvent> | null>(null);

  constructor() {
    combineLatest([
      toObservable(this.filter),
      toObservable(this.pageIndex),
      toObservable(this.pageSize),
    ])
      .pipe(
        tap(() => this.loading.set(true)),
        switchMap(([filter, pageIndex, pageSize]) =>
          this.api
            .searchAuditEvents(filter, pageIndex + 1, pageSize)
            .pipe(catchError(() => of(null))),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        this.result.set(result);
        this.loading.set(false);
      });
  }

  protected update(change: Partial<AuditFilter>): void {
    this.pageIndex.set(0);
    this.filter.update((current) => ({ ...current, ...change }));
  }

  /** Dates are local calendar days: from 00:00 of the first day to 23:59:59.999 of the last day. */
  protected onDate(field: 'from' | 'to', date: Date | null): void {
    if (!date) {
      this.update({ [field]: undefined });
      return;
    }
    const value = new Date(date);
    if (field === 'to') {
      value.setHours(23, 59, 59, 999);
    }
    this.update({ [field]: value.toISOString() });
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.pageIndex.set(event.pageIndex);
  }

  protected open(event: AuditEvent): void {
    this.dialog.open(AuditEventDialog, { data: event, width: 'min(640px, 95vw)' });
  }
}
