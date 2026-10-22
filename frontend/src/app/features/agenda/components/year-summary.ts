import { Component, computed, input, output } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatChipListboxChange, MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { overduePresentation, statusPresentation } from '../data/agenda-labels';
import { EventType, Summary } from '../data/agenda.models';
import { capitalize, monthName } from '../data/agenda-time';
import { AgendaStatus } from './status-indicator';

/**
 * Summary of the year (SPEC-0006 RF-13): the year selector, the twelve months with their number of activities, and the totals
 * by state and by type. It follows the active filters, so with a tag chosen the months show where it appears.
 */
@Component({
  selector: 'app-year-summary',
  imports: [AgendaStatus, MatCardModule, MatChipsModule, MatFormFieldModule, MatSelectModule],
  template: `
    <section class="summary" aria-labelledby="summary-title">
      <div class="head">
        <h2 id="summary-title" class="title">Resumen de {{ year() }}</h2>
        <mat-form-field appearance="outline" class="year">
          <mat-label>Año</mat-label>
          <mat-select [value]="year()" (valueChange)="yearChange.emit($event)">
            @for (option of yearOptions(); track option) {
              <mat-option [value]="option">{{ option }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </div>

      <mat-chip-listbox
        aria-label="Meses del año con su cantidad de actividades"
        [value]="month()"
        (change)="onMonth($event)"
      >
        @for (item of months(); track item.month) {
          <mat-chip-option
            [value]="item.month"
            [highlighted]="item.total > 0"
            [aria-label]="item.aria"
          >
            {{ item.label }} · {{ item.total }}
          </mat-chip-option>
        }
      </mat-chip-listbox>

      <div class="cards">
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Por estado</mat-card-title></mat-card-header>
          <mat-card-content>
            <ul class="totals">
              @for (row of statusRows(); track row.key) {
                <li>
                  <app-agenda-status [status]="row.presentation" />
                  <span class="count">{{ row.count }}</span>
                </li>
              }
            </ul>
          </mat-card-content>
        </mat-card>
        <mat-card appearance="outlined">
          <mat-card-header><mat-card-title>Por tipo</mat-card-title></mat-card-header>
          <mat-card-content>
            @if (typeRows().length > 0) {
              <ul class="totals">
                @for (row of typeRows(); track row.id) {
                  <li>
                    <span class="type">{{ row.name }}</span>
                    <span class="count">{{ row.count }}</span>
                  </li>
                }
              </ul>
            } @else {
              <p class="none">Sin actividades en este año.</p>
            }
          </mat-card-content>
        </mat-card>
      </div>
    </section>
  `,
  styles: `
    :host {
      display: block;
    }
    .head {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 0 16px;
    }
    .title {
      font: var(--mat-sys-title-medium);
      margin: 0;
      flex: 1 1 auto;
    }
    .year {
      width: 140px;
    }
    .cards {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 16px;
      margin: 16px 0;
    }
    .totals {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .totals li {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
    }
    .count {
      font: var(--mat-sys-title-small);
    }
    .type {
      overflow-wrap: anywhere;
    }
    .none {
      color: var(--mat-sys-on-surface-variant);
      margin: 0;
    }
  `,
})
export class YearSummary {
  readonly summary = input<Summary | null>(null);
  readonly year = input.required<number>();
  /** The month the list is narrowed to, or null. */
  readonly month = input<number | null>(null);
  readonly types = input<readonly EventType[]>([]);
  readonly yearChange = output<number>();
  readonly monthChange = output<number | null>();

  protected readonly yearOptions = computed(() => {
    const centre = this.year();
    return Array.from({ length: 11 }, (_, index) => centre - 5 + index);
  });

  protected readonly months = computed(() =>
    Array.from({ length: 12 }, (_, index) => {
      const month = index + 1;
      const row = this.summary()?.months.find((candidate) => candidate.month === month);
      const total = row ? row.draft + row.scheduled + row.completed + row.cancelled : 0;
      const overdue = row?.overdue ?? 0;
      const name = capitalize(monthName(month));
      return {
        month,
        total,
        label: name.slice(0, 3),
        aria: `${name}: ${total} ${total === 1 ? 'actividad' : 'actividades'}${
          overdue > 0 ? `, ${overdue} ${overdue === 1 ? 'vencida' : 'vencidas'}` : ''
        }`,
      };
    }),
  );

  protected readonly statusRows = computed(() => {
    const months = this.summary()?.months ?? [];
    const sum = (pick: (row: (typeof months)[number]) => number) =>
      months.reduce((total, row) => total + pick(row), 0);
    return [
      { key: 'draft', presentation: statusPresentation('draft'), count: sum((r) => r.draft) },
      {
        key: 'scheduled',
        presentation: statusPresentation('scheduled'),
        count: sum((r) => r.scheduled),
      },
      {
        key: 'completed',
        presentation: statusPresentation('completed'),
        count: sum((r) => r.completed),
      },
      {
        key: 'cancelled',
        presentation: statusPresentation('cancelled'),
        count: sum((r) => r.cancelled),
      },
      { key: 'overdue', presentation: overduePresentation(), count: sum((r) => r.overdue) },
    ];
  });

  protected readonly typeRows = computed(() =>
    (this.summary()?.byType ?? []).map((row) => ({
      id: row.typeId,
      name: this.types().find((type) => type.id === row.typeId)?.name ?? 'Tipo desconocido',
      count: row.count,
    })),
  );

  protected onMonth(event: MatChipListboxChange): void {
    this.monthChange.emit(
      event.value === undefined || event.value === null ? null : Number(event.value),
    );
  }
}
