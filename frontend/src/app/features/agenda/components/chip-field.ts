import { COMMA, ENTER } from '@angular/cdk/keycodes';
import { Component, computed, input, model, output, signal } from '@angular/core';
import {
  MatAutocompleteModule,
  MatAutocompleteSelectedEvent,
} from '@angular/material/autocomplete';
import { MatChipInputEvent, MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { ChipResult, appendUnique } from '../data/agenda-fields';

/** A suggestion of the autocomplete: the text added as a chip and the one shown in the list. */
export interface ChipSuggestion {
  readonly value: string;
  readonly label: string;
}

/**
 * Free-text chips with suggestions (SPEC-0006 §11, RF-23): the standard Material pattern of `mat-chip-grid` with
 * `mat-autocomplete`. Used for tags and for participants. Enter (or a comma) adds what was typed; chips are plain text.
 */
@Component({
  selector: 'app-chip-field',
  imports: [MatAutocompleteModule, MatChipsModule, MatFormFieldModule, MatIconModule],
  template: `
    <mat-form-field appearance="outline" class="field">
      <mat-label>{{ label() }}</mat-label>
      <mat-chip-grid #grid [attr.aria-label]="label()">
        @for (value of values(); track value) {
          <mat-chip-row (removed)="remove(value)">
            {{ value }}
            <button matChipRemove [attr.aria-label]="'Quitar ' + itemLabel() + ' ' + value">
              <mat-icon>cancel</mat-icon>
            </button>
          </mat-chip-row>
        }
        <input
          [attr.aria-invalid]="error() ? 'true' : null"
          [attr.aria-describedby]="error() ? errorId : null"
          [placeholder]="placeholder()"
          [disabled]="disabled()"
          [matChipInputFor]="grid"
          [matAutocomplete]="auto"
          [matChipInputSeparatorKeyCodes]="separators"
          [matChipInputAddOnBlur]="true"
          (matChipInputTokenEnd)="add($event)"
          (input)="onInput($any($event.target).value)"
        />
      </mat-chip-grid>
      <mat-autocomplete #auto="matAutocomplete" (optionSelected)="select($event)">
        @for (suggestion of available(); track suggestion.value) {
          <mat-option [value]="suggestion.value">{{ suggestion.label }}</mat-option>
        }
      </mat-autocomplete>
      @if (hint() && !error()) {
        <mat-hint>{{ hint() }}</mat-hint>
      }
    </mat-form-field>
    @if (error(); as message) {
      <p class="error" role="alert" [id]="errorId">{{ message }}</p>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .field {
      width: 100%;
    }
    .error {
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
      margin: -16px 0 12px 16px;
    }
  `,
})
export class ChipField {
  private static nextId = 0;

  readonly label = input.required<string>();
  readonly itemLabel = input('elemento');
  readonly placeholder = input('');
  readonly hint = input('');
  readonly values = model<readonly string[]>([]);
  /** Most chips allowed; the message is shown when somebody tries to add one more. */
  readonly max = input(Number.POSITIVE_INFINITY);
  readonly maxMessage = input('Has alcanzado el máximo.');
  readonly disabled = input(false);
  readonly normalize = input.required<(raw: string) => ChipResult>();
  readonly suggestions = input<readonly ChipSuggestion[]>([]);
  /** Text being typed, so the parent can load suggestions. */
  readonly query = output<string>();

  protected readonly separators = [ENTER, COMMA] as const;
  /** Links the message to the input (`aria-describedby`), so a screen reader reads why the value was refused. */
  protected readonly errorId = `app-chip-field-error-${ChipField.nextId++}`;
  protected readonly error = signal<string | null>(null);
  protected readonly atMax = computed(() => this.values().length >= this.max());
  /** Suggestions that are not chips yet. */
  protected readonly available = computed(() => {
    const taken = new Set(this.values().map((value) => value.toLowerCase()));
    return this.suggestions().filter((suggestion) => !taken.has(suggestion.value.toLowerCase()));
  });

  protected onInput(text: string): void {
    this.error.set(null);
    this.query.emit(text);
  }

  /** Clears what was typed only when it became a chip: a refused address stays there to be corrected. */
  protected add(event: MatChipInputEvent): void {
    if (this.addText(event.value)) {
      event.chipInput.clear();
    }
  }

  protected select(event: MatAutocompleteSelectedEvent): void {
    this.addText(String(event.option.value));
    event.option.deselect();
  }

  protected remove(value: string): void {
    this.error.set(null);
    this.values.set(this.values().filter((existing) => existing !== value));
  }

  /**
   * Adds one chip from text, applying the normalization and the maximum. Returns whether the text was used (added, or empty and
   * ignored), so the caller keeps a refused value in the input.
   */
  addText(raw: string): boolean {
    const result = this.normalize()(raw);
    if (result === null) {
      return true;
    }
    if ('error' in result) {
      this.error.set(result.error);
      return false;
    }
    if (this.atMax()) {
      this.error.set(this.maxMessage());
      return false;
    }
    this.error.set(null);
    this.values.set(appendUnique(this.values(), result.value));
    this.query.emit('');
    return true;
  }
}
