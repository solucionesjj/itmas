import { Component, DestroyRef, inject, input, output } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { AgendaFilters } from '../data/agenda-api';
import { statusLabel } from '../data/agenda-labels';
import { EventType, StatusFilter } from '../data/agenda.models';
import { hasFilters } from '../data/agenda-state';
import { TagsField } from './tags-field';

/** A person offered by the "Responsable" filter. */
export interface PersonFilterOption {
  readonly id: string;
  readonly name: string;
}

/**
 * Filters shared by the views of the agenda (SPEC-0006 RF-12, RF-25): tags (all of them), type, state (including "Vencidas"),
 * responsible, "Mis actividades" and a text search on titles and tags. They combine, and the same bar serves every view.
 */
@Component({
  selector: 'app-agenda-filters',
  imports: [
    MatButtonModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    TagsField,
  ],
  template: `
    <div class="filters" role="search" aria-label="Filtros de la agenda">
      <mat-form-field appearance="outline" class="wide">
        <mat-label>Buscar por título o etiqueta</mat-label>
        <mat-icon matPrefix>search</mat-icon>
        <input
          matInput
          maxlength="100"
          [value]="filters().search"
          (input)="typed.next($any($event.target).value)"
        />
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Tipo</mat-label>
        <mat-select [value]="filters().type" (valueChange)="patch({ type: $event })">
          <mat-option value="">Todos</mat-option>
          @for (type of types(); track type.id) {
            <mat-option [value]="type.id">{{ type.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Estado</mat-label>
        <mat-select [value]="filters().status" (valueChange)="patch({ status: $event })">
          <mat-option value="">Todos</mat-option>
          @for (status of statuses; track status) {
            <mat-option [value]="status">{{ statusText(status) }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline">
        <mat-label>Responsable</mat-label>
        <mat-select [value]="filters().responsible" (valueChange)="patch({ responsible: $event })">
          <mat-option value="">Todos</mat-option>
          @for (person of people(); track person.id) {
            <mat-option [value]="person.id">{{ person.name }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-slide-toggle
        class="mine"
        [checked]="filters().mine"
        (change)="patch({ mine: $event.checked })"
      >
        Mis actividades
      </mat-slide-toggle>
    </div>
    <app-tags-field
      label="Filtrar por etiquetas"
      hint="Se muestran las actividades que tienen todas las etiquetas."
      [max]="10"
      [tags]="filters().tags"
      (tagsChange)="patch({ tags: $event })"
    />
    @if (active()) {
      <button mat-stroked-button type="button" class="clear" (click)="clear()">
        <mat-icon>filter_alt_off</mat-icon>
        Limpiar filtros
      </button>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .filters {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0 16px;
    }
    .filters mat-form-field {
      min-width: 200px;
      flex: 1 1 200px;
      max-width: 320px;
    }
    .filters .wide {
      flex: 2 1 280px;
      max-width: 440px;
    }
    .mine {
      margin-bottom: 20px;
    }
    .clear {
      margin-bottom: 16px;
    }
  `,
})
export class AgendaFiltersBar {
  readonly filters = input.required<AgendaFilters>();
  readonly types = input<readonly EventType[]>([]);
  readonly people = input<readonly PersonFilterOption[]>([]);
  readonly filtersChange = output<AgendaFilters>();

  protected readonly statuses: readonly StatusFilter[] = [
    'draft',
    'scheduled',
    'completed',
    'cancelled',
    'overdue',
  ];
  protected readonly typed = new Subject<string>();

  constructor() {
    this.typed
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(inject(DestroyRef)))
      .subscribe((text) => this.patch({ search: text.trim() }));
  }

  protected active(): boolean {
    return hasFilters(this.filters());
  }

  protected statusText(status: StatusFilter): string {
    return status === 'overdue' ? 'Vencidas' : statusLabel(status);
  }

  protected patch(change: Partial<AgendaFilters>): void {
    this.filtersChange.emit({ ...this.filters(), ...change });
  }

  protected clear(): void {
    this.filtersChange.emit({
      type: '',
      status: '',
      responsible: '',
      mine: false,
      tags: [],
      search: '',
    });
  }
}
