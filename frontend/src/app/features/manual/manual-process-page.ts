import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { Subject, catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { MANUAL_PATH, ManualApi } from './data/manual-api';
import { renderProcess } from './data/manual-text';
import { ManualProcess } from './data/manual.models';

type ProcessState =
  | { readonly status: 'loading' | 'error' | 'missing' }
  | {
      readonly status: 'ready';
      readonly process: ManualProcess;
      readonly category: string;
      readonly html: string;
    };

/** One process of the user manual, at a shareable URL (SPEC-0004 RF-4). Public page. */
@Component({
  selector: 'app-manual-process-page',
  imports: [
    DatePipe,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatProgressBarModule,
    RouterLink,
  ],
  template: `
    <div class="page-header">
      <a mat-icon-button routerLink="/manual" aria-label="Volver al índice del manual">
        <mat-icon>arrow_back</mat-icon>
      </a>
      <h1 class="page-title">{{ ready()?.process.title ?? 'Manual de uso' }}</h1>
    </div>

    @switch (state().status) {
      @case ('loading') {
        <mat-progress-bar mode="indeterminate" aria-label="Cargando el proceso" />
      }
      @case ('missing') {
        <p class="empty">
          Este proceso no existe o se ha retirado del manual.
          <a mat-button routerLink="/manual">Ir al índice del manual</a>
        </p>
      }
      @case ('error') {
        <div class="error-state" role="alert">
          <span>No se pudo cargar el proceso.</span>
          <button mat-stroked-button (click)="retry()">
            <mat-icon>refresh</mat-icon>
            Reintentar
          </button>
        </div>
      }
    }

    @if (ready(); as page) {
      <div class="meta supporting-text">
        <span>{{ page.category }}</span>
        <mat-chip-set aria-label="Permisos necesarios">
          @for (permission of page.process.permissions; track permission) {
            <mat-chip [disableRipple]="true">{{ permission }}</mat-chip>
          } @empty {
            <mat-chip [disableRipple]="true">Cualquier usuario</mat-chip>
          }
        </mat-chip-set>
        <span
          >Actualizado el {{ page.process.updated | date: 'longDate' }} ({{
            page.process.specs.join(', ')
          }})</span
        >
      </div>
      <article class="content" [innerHTML]="page.html" (click)="followLink($event)"></article>
    }
  `,
  styleUrl: './manual-page.scss',
})
export class ManualProcessPage {
  private readonly api = inject(ManualApi);
  private readonly router = inject(Router);
  private readonly reload = new Subject<void>();

  readonly id = input.required<string>();

  protected readonly state = toSignal(
    combineLatest([toObservable(this.id), this.reload.pipe(startWith(undefined))]).pipe(
      switchMap(([id]) =>
        this.api.getIndex().pipe(
          switchMap((index) => {
            // Only ids listed in the index are fetched: the URL never chooses an arbitrary file.
            const process = index.processes.find((candidate) => candidate.id === id);
            if (!process) {
              return of<ProcessState>({ status: 'missing' });
            }
            const category =
              index.categories.find((item) => item.id === process.category)?.title ?? '';
            return this.api.getContent(process.id).pipe(
              map((source): ProcessState => ({
                status: 'ready',
                process,
                category,
                html: renderProcess(source),
              })),
            );
          }),
          catchError(() => {
            this.api.reset();
            return of<ProcessState>({ status: 'error' });
          }),
          startWith<ProcessState>({ status: 'loading' }),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as ProcessState },
  );

  protected readonly ready = computed(() => {
    const state = this.state();
    return state.status === 'ready' ? state : null;
  });

  protected retry(): void {
    this.reload.next();
  }

  /** Links to other processes navigate inside the SPA instead of reloading the page. */
  protected followLink(event: MouseEvent): void {
    const anchor = (event.target as HTMLElement | null)?.closest('a');
    const href = anchor?.getAttribute('href');
    if (
      href?.startsWith(`${MANUAL_PATH}/`) &&
      !event.ctrlKey &&
      !event.metaKey &&
      !event.shiftKey &&
      event.button === 0
    ) {
      event.preventDefault();
      void this.router.navigateByUrl(href);
    }
  }
}
