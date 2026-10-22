import { Component, DestroyRef, inject, input, model, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subject, catchError, debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { AgendaApi } from '../data/agenda-api';
import { normalizeTag } from '../data/agenda-fields';
import { LIMITS, TagCount } from '../data/agenda.models';
import { ChipField, ChipSuggestion } from './chip-field';

/**
 * Tags of an activity or of a filter (SPEC-0006 RF-23, §8.4): free text with suggestions of the existing tags and how many
 * activities use each one, so they are reused instead of duplicated.
 */
@Component({
  selector: 'app-tags-field',
  imports: [ChipField],
  template: `
    <app-chip-field
      [label]="label()"
      itemLabel="etiqueta"
      [hint]="hint()"
      [max]="max()"
      [maxMessage]="maxMessage"
      [normalize]="normalize"
      [suggestions]="suggestions()"
      [(values)]="tags"
      (query)="search.next($event)"
    />
  `,
})
export class TagsField {
  private readonly api = inject(AgendaApi);

  readonly tags = model<readonly string[]>([]);
  readonly label = input('Etiquetas');
  readonly hint = input('Escribe una etiqueta y pulsa Intro');
  readonly max = input<number>(LIMITS.tags);

  protected readonly maxMessage = `Máximo ${LIMITS.tags} etiquetas por actividad.`;
  protected readonly normalize = normalizeTag;
  protected readonly suggestions = signal<readonly ChipSuggestion[]>([]);
  protected readonly search = new Subject<string>();

  constructor() {
    this.search
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((text) =>
          this.api.listTags(text.trim(), 10).pipe(catchError(() => of([] as readonly TagCount[]))),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((tags) =>
        this.suggestions.set(
          tags.map((tag) => ({ value: tag.name, label: `${tag.name} (${tag.count})` })),
        ),
      );
    // The existing tags are offered as soon as the field is shown.
    this.search.next('');
  }
}
