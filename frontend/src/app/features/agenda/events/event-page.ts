import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { filter, forkJoin, of } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { problemMessage } from '../../../core/models/problem-message';
import { NotificationService } from '../../../core/services/notification-service';
import { confirm } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { AgendaApi } from '../data/agenda-api';
import { isOpenOccurrence, isRealDate } from '../data/agenda-time';
import { EventDetail, EventType } from '../data/agenda.models';
import { EventFields } from './event-fields';
import {
  createEventForm,
  patchEventForm,
  setCommonOnly,
  stringToDate,
  toEventInput,
  toSeriesCommonInput,
} from './event-form';

/**
 * Creates (`/agenda/events/new`) or edits (`/agenda/events/:id/edit`) an activity (SPEC-0006 RF-3, RF-5). With `?scope=series`
 * on an open occurrence of a series it edits the common fields of the whole series instead (RF-6, P-29). A new activity can
 * start from a day or an hour with `?date=` and `?time=` (P-21).
 */
@Component({
  selector: 'app-event-page',
  imports: [EventFields, MatButtonModule, MatIconModule, MatProgressBarModule, RouterLink],
  templateUrl: './event-page.html',
  styleUrl: '../agenda-page.scss',
})
export class EventPage {
  private readonly api = inject(AgendaApi);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  /** Route parameter; absent when creating. */
  readonly id = input<string>();
  /** `series` edits the common fields of the series of the activity. */
  readonly scope = input<string>();
  /** Day (`yyyy-MM-dd`) and hour (`HH:mm`) a new activity starts on, when it comes from an empty slot of a calendar. */
  readonly date = input<string>();
  readonly time = input<string>();

  protected readonly form = createEventForm();
  protected readonly types = signal<readonly EventType[]>([]);
  protected readonly event = signal<EventDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly conflict = signal(false);
  private etag = '';

  protected readonly isEditing = computed(() => !!this.id());
  protected readonly closed = computed(() => {
    const status = this.event()?.status;
    return status === 'completed' || status === 'cancelled';
  });
  /** The common fields of a series are edited only from an open occurrence of it. */
  protected readonly canEditSeries = computed(() => {
    const event = this.event();
    return !!event?.seriesId && isOpenOccurrence(event);
  });
  protected readonly seriesMode = computed(() => this.scope() === 'series' && this.canEditSeries());
  /**
   * Changing a scheduled activity notifies its guests, so it needs `agenda.publish` as well (P-12): without it the form is not
   * offered. A series is checked by the API, which knows whether any of its occurrences is scheduled.
   */
  protected readonly needsPublish = computed(
    () =>
      this.isEditing() &&
      !this.seriesMode() &&
      this.event()?.status === 'scheduled' &&
      !this.auth.hasPermission('agenda.publish'),
  );
  protected readonly heading = computed(() =>
    this.seriesMode()
      ? 'Editar la serie'
      : this.isEditing()
        ? 'Editar actividad'
        : 'Nueva actividad',
  );
  protected readonly saveLabel = computed(() =>
    this.seriesMode()
      ? 'Aplicar a la serie'
      : this.isEditing()
        ? 'Guardar cambios'
        : 'Crear actividad',
  );
  protected readonly backLink = computed(() =>
    this.isEditing() ? ['/agenda/events', this.id()] : ['/agenda'],
  );
  /** Active types, plus the current one of an activity whose type was deactivated afterwards (RF-2). */
  protected readonly selectableTypes = computed(() => {
    const current = this.event()?.type.id;
    return this.types().filter((type) => type.isActive || type.id === current);
  });

  constructor() {
    effect(() => this.load(this.id()));
  }

  protected reload(): void {
    this.load(this.id());
  }

  protected save(): void {
    if (this.saving()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.error.set('Revisa los campos marcados.');
      return;
    }

    this.error.set(null);
    this.conflict.set(false);
    if (this.seriesMode()) {
      this.saveSeries();
    } else {
      this.saveEvent();
    }
  }

  private saveEvent(): void {
    const id = this.id();
    const input = toEventInput(this.form);
    this.saving.set(true);
    const request = id ? this.api.updateEvent(id, this.etag, input) : this.api.createEvent(input);
    request.subscribe({
      next: ({ event }) => {
        this.saving.set(false);
        this.notifications.info(id ? 'Actividad actualizada.' : 'Actividad creada en borrador.');
        void this.router.navigate(['/agenda/events', event.id]);
      },
      error: (error: unknown) => this.fail(error),
    });
  }

  /** Asks for the confirmation of §8.4 first: the change reaches every open occurrence. */
  private saveSeries(): void {
    const event = this.event();
    const seriesId = event?.seriesId;
    if (!event || !seriesId) {
      return;
    }
    confirm(this.dialog, {
      title: 'Editar la serie',
      message:
        'El cambio se aplicará a las ocurrencias de la serie que aún no han empezado. Las fechas no cambian.',
      confirmLabel: 'Aplicar a la serie',
    })
      .pipe(filter((confirmed) => confirmed === true))
      .subscribe(() => {
        this.saving.set(true);
        this.api.updateSeries(seriesId, toSeriesCommonInput(this.form)).subscribe({
          next: ({ updated }) => {
            this.saving.set(false);
            this.notifications.info(
              updated === 1
                ? 'Se actualizó 1 actividad de la serie.'
                : `Se actualizaron ${updated} actividades de la serie.`,
            );
            void this.router.navigate(['/agenda/events', event.id]);
          },
          error: (error: unknown) => this.fail(error),
        });
      });
  }

  private fail(error: unknown): void {
    this.saving.set(false);
    if (error instanceof ApiError && error.status === 412) {
      this.conflict.set(true);
      this.error.set(
        'Otro usuario modificó esta actividad. Recarga los datos e inténtalo de nuevo.',
      );
      return;
    }
    this.error.set(problemMessage(error));
  }

  private load(id: string | undefined): void {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.error.set(null);
    this.conflict.set(false);

    forkJoin({
      types: this.api.listTypes(),
      loaded: id ? this.api.getEvent(id) : of(null),
    }).subscribe({
      next: ({ types, loaded }) => {
        this.types.set(types);
        if (loaded) {
          this.event.set(loaded.event);
          this.etag = loaded.etag;
          patchEventForm(this.form, loaded.event);
          setCommonOnly(this.form, this.seriesMode());
        } else {
          this.prefillFromSlot();
        }
        this.loading.set(false);
      },
      error: () => {
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  /** A new activity that comes from an empty day or hour of a calendar starts there (RF-12b). */
  private prefillFromSlot(): void {
    const date = this.date();
    if (date && isRealDate(date)) {
      this.form.controls.startDate.setValue(stringToDate(date));
    }
    const time = this.time();
    if (time && /^([01]\d|2[0-3]):[0-5]\d$/.test(time)) {
      this.form.controls.startTime.setValue(time);
    }
  }
}
