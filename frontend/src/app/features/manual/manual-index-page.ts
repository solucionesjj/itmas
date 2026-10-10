import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import { Subject, catchError, map, of, startWith, switchMap } from 'rxjs';
import { ManualApi } from './data/manual-api';
import { matches } from './data/manual-text';
import { ManualIndex } from './data/manual.models';

type IndexState =
  | { readonly status: 'loading' | 'error' }
  | { readonly status: 'ready'; readonly index: ManualIndex };

/** Index of the user manual grouped by category, with a text search (SPEC-0004 RF-2, RF-3, RF-6). Public page. */
@Component({
  selector: 'app-manual-index-page',
  imports: [
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    RouterLink,
  ],
  template: `
    <div class="page-header">
      <h1 class="page-title">Manual de uso</h1>
    </div>
    <p class="supporting-text">
      Procesos de ITMas: qué se puede hacer, quién puede hacerlo y cómo. Se actualiza con cada
      funcionalidad nueva.
    </p>

    <mat-form-field appearance="outline" class="search">
      <mat-label>Buscar en el manual</mat-label>
      <mat-icon matPrefix>search</mat-icon>
      <input
        matInput
        maxlength="100"
        [value]="query()"
        (input)="query.set($any($event.target).value)"
      />
    </mat-form-field>

    @switch (state().status) {
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Cargando el manual" />
      }
      @case ('error') {
        <div class="error-state" role="alert">
          <span>No se pudo cargar el manual.</span>
          <button mat-stroked-button (click)="retry()">
            <mat-icon>refresh</mat-icon>
            Reintentar
          </button>
        </div>
      }
      @default {
        @for (group of groups(); track group.id) {
          <section class="category" [attr.aria-labelledby]="'category-' + group.id">
            <h2 class="category-title" [id]="'category-' + group.id">{{ group.title }}</h2>
            <div class="processes">
              @for (process of group.processes; track process.id) {
                <a class="process-card" [routerLink]="['/manual', process.id]">
                  <mat-card appearance="outlined">
                    <mat-card-header>
                      <mat-card-title>{{ process.title }}</mat-card-title>
                    </mat-card-header>
                    <mat-card-content>
                      <p>{{ process.summary }}</p>
                      <mat-chip-set [attr.aria-label]="'Permisos necesarios para ' + process.title">
                        @for (permission of process.permissions; track permission) {
                          <mat-chip [disableRipple]="true">{{ permission }}</mat-chip>
                        } @empty {
                          <mat-chip [disableRipple]="true">Cualquier usuario</mat-chip>
                        }
                      </mat-chip-set>
                    </mat-card-content>
                  </mat-card>
                </a>
              }
            </div>
          </section>
        } @empty {
          <p class="empty">Ningún proceso coincide con la búsqueda.</p>
        }
      }
    }
  `,
  styleUrl: './manual-page.scss',
})
export class ManualIndexPage {
  private readonly api = inject(ManualApi);
  private readonly reload = new Subject<void>();

  protected readonly query = signal('');

  protected readonly state = toSignal(
    this.reload.pipe(
      startWith(undefined),
      switchMap(() =>
        this.api.getIndex().pipe(
          map((index): IndexState => ({ status: 'ready', index })),
          catchError(() => {
            this.api.reset();
            return of<IndexState>({ status: 'error' });
          }),
          startWith<IndexState>({ status: 'loading' }),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as IndexState },
  );

  /** Categories in index order, each with the processes that match the search; empty categories are hidden. */
  protected readonly groups = computed(() => {
    const state = this.state();
    if (state.status !== 'ready') {
      return [];
    }
    const query = this.query();
    return state.index.categories
      .map((category) => ({
        ...category,
        processes: state.index.processes.filter(
          (process) => process.category === category.id && matches(process, category.title, query),
        ),
      }))
      .filter((category) => category.processes.length > 0);
  });

  protected retry(): void {
    this.reload.next();
  }
}
