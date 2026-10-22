import { Component, computed, inject, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { Router, RouterLink } from '@angular/router';
import { problemMessage } from '../../../core/models/problem-message';
import { NotificationService } from '../../../core/services/notification-service';
import { WarningList } from '../components/warning-list';
import { AgendaApi } from '../data/agenda-api';
import {
  NTHS,
  UNITS,
  WEEKDAYS,
  ordinalLabel,
  unitLabel,
  weekdayLabel,
} from '../data/agenda-labels';
import { whenLabel } from '../data/agenda-time';
import { EventType, LIMITS, SeriesOccurrence } from '../data/agenda.models';
import { EventFields } from '../events/event-fields';
import { createEventForm } from '../events/event-form';
import { createRuleForm, toSeriesInput } from './series-form';

/**
 * Creates a series (`/agenda/series/new`, SPEC-0006 RF-4, §8.1): the data of the first activity and a repetition rule. Before
 * confirming, the page shows the dates the series would have and their warnings; nothing is saved by the preview. A change
 * to the form invalidates the preview, so what is created is always what was shown.
 */
@Component({
  selector: 'app-series-page',
  imports: [
    EventFields,
    MatButtonModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatRadioModule,
    MatSelectModule,
    MatTableModule,
    ReactiveFormsModule,
    RouterLink,
    WarningList,
  ],
  templateUrl: './series-page.html',
  styleUrls: ['../agenda-page.scss', './series-page.scss'],
})
export class SeriesPage {
  private readonly api = inject(AgendaApi);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);

  protected readonly eventForm = createEventForm();
  protected readonly rule = createRuleForm();

  protected readonly types = signal<readonly EventType[]>([]);
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly working = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly preview = signal<readonly SeriesOccurrence[] | null>(null);
  /** The request the preview was made for: the series is created only while the form still matches it. */
  private previewKey = '';

  protected readonly activeTypes = computed(() => this.types().filter((type) => type.isActive));
  protected readonly units = UNITS;
  protected readonly nths = NTHS;
  protected readonly weekdays = WEEKDAYS;
  protected readonly maxOccurrences = LIMITS.maxOccurrences;
  protected readonly columns = ['number', 'when', 'warnings'];
  protected readonly unitText = unitLabel;
  protected readonly ordinalText = ordinalLabel;
  protected readonly weekdayText = weekdayLabel;
  protected readonly whenOf = whenLabel;

  constructor() {
    this.loadTypes();
  }

  protected loadTypes(): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.api.listTypes().subscribe({
      next: (types) => {
        this.types.set(types);
        this.loading.set(false);
      },
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  protected isMonthly(): boolean {
    const unit = this.rule.controls.unit.value;
    return unit === 'month' || unit === 'year';
  }

  protected warningCount(): number {
    return (this.preview() ?? []).filter((occurrence) => occurrence.warnings.length > 0).length;
  }

  /** The preview shown matches what the form says now, so it can be confirmed. */
  protected previewIsCurrent(): boolean {
    return this.preview() !== null && this.previewKey === this.currentKey();
  }

  protected runPreview(): void {
    if (!this.validate() || this.working()) {
      return;
    }
    this.working.set(true);
    const key = this.currentKey();
    this.api.previewSeries(toSeriesInput(this.eventForm, this.rule)).subscribe({
      next: (occurrences) => {
        this.working.set(false);
        this.preview.set(occurrences);
        this.previewKey = key;
      },
      error: (error: unknown) => {
        this.working.set(false);
        this.preview.set(null);
        this.error.set(problemMessage(error));
      },
    });
  }

  protected create(): void {
    if (!this.previewIsCurrent() || !this.validate() || this.working()) {
      return;
    }
    const firstYear = this.preview()?.[0]?.start.slice(0, 4);
    this.working.set(true);
    this.api.createSeries(toSeriesInput(this.eventForm, this.rule)).subscribe({
      next: (created) => {
        this.working.set(false);
        const count = created.eventIds.length;
        this.notifications.info(
          count === 1
            ? 'Serie creada con 1 actividad en borrador.'
            : `Serie creada con ${count} actividades en borrador.`,
        );
        void this.router.navigate(['/agenda'], {
          queryParams: firstYear ? { year: firstYear } : {},
        });
      },
      error: (error: unknown) => {
        this.working.set(false);
        this.error.set(problemMessage(error));
      },
    });
  }

  private validate(): boolean {
    this.error.set(null);
    this.eventForm.markAllAsTouched();
    this.rule.markAllAsTouched();
    if (this.eventForm.invalid || this.rule.invalid) {
      this.error.set('Revisa los campos marcados.');
      return false;
    }
    return true;
  }

  private currentKey(): string {
    return JSON.stringify(toSeriesInput(this.eventForm, this.rule));
  }
}
