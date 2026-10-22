import { Component, DestroyRef, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { Subject, catchError, map, of, switchMap, tap } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { problemMessage } from '../../../core/models/problem-message';
import { EventStatusChips } from '../components/event-status-chips';
import { OutlookStatusIndicator } from '../components/outlook-status-indicator';
import { TagsField } from '../components/tags-field';
import { WarningList } from '../components/warning-list';
import { AgendaApi } from '../data/agenda-api';
import {
  impactLabel,
  outlookErrorText,
  reminderLabel,
  resultLabel,
  ruleLabel,
} from '../data/agenda-labels';
import { instantLabel, isOpenOccurrence, whenLabel } from '../data/agenda-time';
import { EventDetail, VersionedEvent } from '../data/agenda.models';
import { AgendaActions } from '../events/agenda-actions';

type LoadFailure = 'not-found' | 'error';

/**
 * Detail of an activity (SPEC-0006 RF-14, §11): every datum, the execution record, who created and changed it, its state in
 * Outlook and the actions its state and the permissions of the person allow. Tags are edited here and saved at once (RF-24).
 * Everything a person typed is shown as text, never as HTML (RNF-7).
 */
@Component({
  selector: 'app-event-detail-page',
  imports: [
    EventStatusChips,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    OutlookStatusIndicator,
    RouterLink,
    TagsField,
    WarningList,
  ],
  templateUrl: './event-detail-page.html',
  styleUrls: ['../agenda-page.scss', './event-detail-page.scss'],
})
export class EventDetailPage {
  private readonly api = inject(AgendaApi);
  private readonly actions = inject(AgendaActions);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  /** Route parameter. */
  readonly id = input.required<string>();

  protected readonly event = signal<EventDetail | null>(null);
  protected readonly loading = signal(true);
  protected readonly failure = signal<LoadFailure | null>(null);

  protected readonly editingTags = signal(false);
  /** The tags in the editor: they follow what the person types while the last save is on its way. */
  protected readonly draftTags = signal<readonly string[]>([]);
  protected readonly savingTags = signal(false);
  protected readonly tagsError = signal<string | null>(null);
  private readonly tagSaves = new Subject<readonly string[]>();

  protected readonly whenOf = whenLabel;
  protected readonly impactText = impactLabel;
  protected readonly reminderText = reminderLabel;
  protected readonly resultText = resultLabel;
  protected readonly outlookError = outlookErrorText;
  protected readonly ruleText = ruleLabel;

  private readonly open = computed(() => {
    const event = this.event();
    return !!event && isOpenOccurrence(event);
  });
  private readonly status = computed(() => this.event()?.status);

  /** Changing a scheduled activity notifies its guests, so it needs `agenda.publish` as well (P-12). */
  protected readonly canEdit = computed(
    () =>
      this.auth.hasPermission('agenda.update') &&
      (this.status() === 'draft' ||
        (this.status() === 'scheduled' && this.auth.hasPermission('agenda.publish'))),
  );
  protected readonly canEditSeries = computed(
    () => this.canEdit() && !!this.event()?.seriesId && this.open(),
  );
  protected readonly canSchedule = computed(
    () => this.auth.hasPermission('agenda.publish') && this.status() === 'draft',
  );
  protected readonly canCancel = computed(
    () => this.auth.hasPermission('agenda.publish') && this.status() === 'scheduled',
  );
  protected readonly canComplete = computed(
    () => this.auth.hasPermission('agenda.complete') && this.status() === 'scheduled',
  );
  protected readonly canEditCompletion = computed(
    () => this.auth.hasPermission('agenda.complete') && this.status() === 'completed',
  );
  protected readonly canDelete = computed(
    () => this.auth.hasPermission('agenda.update') && this.status() === 'draft',
  );
  protected readonly canEditTags = computed(() => this.auth.hasPermission('agenda.update'));
  /** "Sincronizar de nuevo" only makes sense for a scheduled activity Outlook has not confirmed, with Outlook on (RF-19). */
  protected readonly canSync = computed(() => {
    const event = this.event();
    return (
      !!event &&
      this.auth.hasPermission('agenda.publish') &&
      event.outlookEnabled &&
      event.status === 'scheduled' &&
      event.outlook.status !== 'published'
    );
  });
  /** An execution can be recorded from the scheduled start on (RN-8). */
  protected readonly hasStarted = computed(() => {
    const event = this.event();
    return !!event && Date.parse(event.startUtc) <= Date.now();
  });

  constructor() {
    effect(() => this.load(this.id()));

    this.tagSaves
      .pipe(
        tap(() => {
          this.savingTags.set(true);
          this.tagsError.set(null);
        }),
        switchMap((tags) =>
          this.api.setTags(this.id(), tags).pipe(
            map((saved) => ({ saved })),
            catchError((error: unknown) => of({ error })),
          ),
        ),
        takeUntilDestroyed(inject(DestroyRef)),
      )
      .subscribe((result) => {
        this.savingTags.set(false);
        if ('saved' in result) {
          this.event.update((event) => (event ? { ...event, tags: result.saved } : event));
          this.draftTags.set(result.saved);
        } else {
          this.tagsError.set(problemMessage(result.error, 'No se pudieron guardar las etiquetas.'));
          this.draftTags.set(this.event()?.tags ?? []);
        }
      });
  }

  protected reload(): void {
    this.load(this.id());
  }

  // ---- Tags -------------------------------------------------------------------------------------------------------

  protected editTags(): void {
    this.draftTags.set(this.event()?.tags ?? []);
    this.tagsError.set(null);
    this.editingTags.set(true);
  }

  protected doneEditingTags(): void {
    this.editingTags.set(false);
  }

  /** Every change is saved at once: the last one wins (RF-24). */
  protected changeTags(tags: readonly string[]): void {
    this.draftTags.set(tags);
    this.tagSaves.next(tags);
  }

  protected filterByTag(tag: string): void {
    void this.router.navigate(['/agenda'], { queryParams: { tags: [tag] } });
  }

  // ---- Actions ----------------------------------------------------------------------------------------------------

  protected schedule(event: EventDetail): void {
    this.actions.schedule([event], event.outlookEnabled).subscribe((changed) => {
      if (changed) {
        this.reload();
      }
    });
  }

  protected cancel(event: EventDetail): void {
    this.actions.cancel(event, event.outlookEnabled).subscribe((result) => this.apply(result));
  }

  protected complete(event: EventDetail): void {
    this.actions
      .complete(event, event.timeZone, event.completion)
      .subscribe((result) => this.apply(result));
  }

  protected sync(event: EventDetail): void {
    this.actions.sync(event).subscribe(() => this.reload());
  }

  protected deleteDraft(event: EventDetail): void {
    this.actions.deleteDraft(event).subscribe(() => void this.router.navigate(['/agenda']));
  }

  protected instant(value: string | null | undefined): string {
    return instantLabel(value, this.event()?.timeZone ?? 'America/Bogota');
  }

  /** A link is drawn only when it is http or https, whatever the API says: the person never follows anything else. */
  protected safeHref(url: string): string | null {
    return /^https?:\/\//i.test(url) ? url : null;
  }

  private apply(result: VersionedEvent): void {
    this.event.set(result.event);
  }

  private load(id: string): void {
    this.loading.set(true);
    this.failure.set(null);
    this.editingTags.set(false);
    this.api.getEvent(id).subscribe({
      next: ({ event }) => {
        this.event.set(event);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.event.set(null);
        this.failure.set(error instanceof ApiError && error.status === 404 ? 'not-found' : 'error');
        this.loading.set(false);
      },
    });
  }
}
