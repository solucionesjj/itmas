import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { ManualIndex } from './manual.models';

/** Base path of the published manual (`frontend/public/manual/`). Static files: no API, no session. */
export const MANUAL_PATH = '/manual';

/** Reads the user manual (SPEC-0004). The index is loaded once per page load and shared. */
@Injectable({ providedIn: 'root' })
export class ManualApi {
  private readonly http = inject(HttpClient);
  private index$: Observable<ManualIndex> | null = null;

  getIndex(): Observable<ManualIndex> {
    this.index$ ??= this.http
      .get<ManualIndex>(`${MANUAL_PATH}/index.json`)
      .pipe(shareReplay({ bufferSize: 1, refCount: false }));
    return this.index$;
  }

  /** Markdown of a process; `id` must come from the index (it is never built from user input). */
  getContent(id: string): Observable<string> {
    return this.http.get(`${MANUAL_PATH}/${encodeURIComponent(id)}.md`, { responseType: 'text' });
  }

  /** Lets a failed load be retried. */
  reset(): void {
    this.index$ = null;
  }
}
