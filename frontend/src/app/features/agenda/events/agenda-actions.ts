import { Injectable, inject } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { EMPTY, Observable, catchError, filter, map, of, switchMap, tap } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { problemMessage } from '../../../core/models/problem-message';
import { FileDownload } from '../../../core/services/file-download';
import { NotificationService } from '../../../core/services/notification-service';
import { confirm } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { AgendaApi, AgendaFilters } from '../data/agenda-api';
import { ExportTarget } from '../data/agenda-state';
import { outlookErrorText } from '../data/agenda-labels';
import { Completion, OutlookState, ScheduleResult, VersionedEvent } from '../data/agenda.models';
import { CancelDialog, CancelDialogData } from './cancel-dialog';
import { CompleteDialog, CompleteDialogData } from './complete-dialog';

/** The data an action needs from an activity: enough to word the dialogs and to call the API. */
export interface ActionTarget {
  readonly id: string;
  readonly title: string;
}

/** Failures the HTTP interceptor already tells the person about (network, permissions, throttling, server): see `httpErrorInterceptor`. */
const EXPLAINED_BY_INTERCEPTOR = (status: number): boolean =>
  status === 0 || status === 403 || status === 429 || status >= 500;

/**
 * Operations on activities shared by the list and the detail (SPEC-0006 §8.4): they ask for the confirmation or the data
 * the specification requires, call the API and tell the person what happened. Each one emits `true` (or the new version)
 * only when something changed, so the screen knows to reload.
 */
@Injectable({ providedIn: 'root' })
export class AgendaActions {
  private readonly api = inject(AgendaApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  private readonly download = inject(FileDownload);

  /** Schedules drafts after a confirmation. With Outlook active, the guests receive their invitations (§8.4). */
  schedule(targets: readonly ActionTarget[], outlookEnabled: boolean): Observable<boolean> {
    const plural = targets.length !== 1;
    return confirm(this.dialog, {
      title: plural ? `Programar ${targets.length} actividades` : 'Programar actividad',
      message: outlookEnabled
        ? 'Se enviarán invitaciones de Outlook a los invitados de cada actividad.'
        : 'Las actividades pasarán a Programada. Outlook no está configurado: no se enviarán invitaciones.',
      confirmLabel: 'Programar',
    }).pipe(
      filter((confirmed) => confirmed === true),
      switchMap(() => this.api.schedule(targets.map((target) => target.id))),
      tap((results) => this.reportScheduling(results, targets)),
      map(() => true),
      catchError((error: unknown) => {
        this.notifications.error(problemMessage(error, 'No se pudo programar.'));
        return of(false);
      }),
    );
  }

  /** Asks for the reason and cancels. Emits the new version of the activity, or nothing if the person backs out. */
  cancel(target: ActionTarget, outlookEnabled: boolean): Observable<VersionedEvent> {
    return this.dialog
      .open<CancelDialog, CancelDialogData, VersionedEvent>(CancelDialog, {
        data: { id: target.id, title: target.title, outlookEnabled },
      })
      .afterClosed()
      .pipe(
        filter((result): result is VersionedEvent => !!result),
        tap((result) => {
          const failed = result.event.outlook.status === 'error';
          if (failed) {
            this.notifications.error(
              'Actividad cancelada, pero no se pudo cancelar en Outlook. Usa «Sincronizar de nuevo».',
            );
          } else {
            this.notifications.info('Actividad cancelada.');
          }
        }),
      );
  }

  /** Records the execution (or edits notes and links when it was already recorded). */
  complete(
    target: ActionTarget,
    timeZone: string,
    completion: Completion | null,
  ): Observable<VersionedEvent> {
    return this.dialog
      .open<CompleteDialog, CompleteDialogData, VersionedEvent>(CompleteDialog, {
        data: { id: target.id, title: target.title, timeZone, completion },
      })
      .afterClosed()
      .pipe(
        filter((result): result is VersionedEvent => !!result),
        tap(() =>
          this.notifications.info(completion ? 'Registro actualizado.' : 'Ejecución registrada.'),
        ),
      );
  }

  /** Deletes a draft after a confirmation. Scheduled activities are cancelled instead (RF-7). */
  deleteDraft(target: ActionTarget): Observable<boolean> {
    return confirm(this.dialog, {
      title: 'Eliminar borrador',
      message: `Se eliminará «${target.title}». Esta acción no se puede deshacer.`,
      confirmLabel: 'Eliminar',
    }).pipe(
      filter((confirmed) => confirmed === true),
      switchMap(() => this.api.deleteEvent(target.id)),
      tap(() => this.notifications.info('Borrador eliminado.')),
      map(() => true),
      catchError((error: unknown) => {
        this.notifications.error(problemMessage(error));
        return EMPTY;
      }),
    );
  }

  /** "Sincronizar de nuevo" (RF-19): retries the publication of one activity in Outlook. */
  sync(target: ActionTarget): Observable<OutlookState> {
    return this.api.syncOutlook(target.id).pipe(
      tap((outlook) => this.reportSync(outlook)),
      catchError((error: unknown) => {
        this.notifications.error(problemMessage(error, 'No se pudo sincronizar con Outlook.'));
        return EMPTY;
      }),
    );
  }

  /** Retries every scheduled activity that has not started and is pending, in error or not published. */
  syncAll(): Observable<boolean> {
    return this.api.syncAllOutlook().pipe(
      tap((results) => {
        const failed = results.filter((result) => result.outlook.status === 'error').length;
        const pending = results.filter((result) => result.outlook.status === 'pending').length;
        if (results.length === 0) {
          this.notifications.info('No hay actividades pendientes de sincronizar.');
        } else if (failed + pending > 0) {
          this.notifications.error(
            `Se sincronizaron ${results.length - failed - pending} de ${results.length} actividades. Las demás siguen pendientes: vuelve a intentarlo.`,
          );
        } else {
          this.notifications.info(
            results.length === 1
              ? 'Actividad sincronizada.'
              : `${results.length} actividades sincronizadas.`,
          );
        }
      }),
      map(() => true),
      catchError((error: unknown) => {
        this.notifications.error(problemMessage(error, 'No se pudo sincronizar con Outlook.'));
        return EMPTY;
      }),
    );
  }

  /**
   * Downloads the scheduled activities of the range and filters as an `.ics` file (RF-21) and says so. Emits `true` when the
   * file was handed to the browser and `false` when it was not, after telling the person why: more than the API can export
   * (`400`) has its own advice; network, permission, throttling and server errors were already explained by the HTTP
   * interceptor; anything else gets the general message.
   */
  exportCalendar(target: ExportTarget, filters: AgendaFilters): Observable<boolean> {
    return this.api.exportCalendar(target.from, target.to, filters).pipe(
      tap((file) => {
        this.download.save(file, target.fileName);
        this.notifications.info(
          `Se descargó «${target.fileName}». Incluye solo las actividades programadas.`,
        );
      }),
      map(() => true),
      catchError((error: unknown) => {
        if (error instanceof ApiError && error.status === 400) {
          this.notifications.error(
            'Hay más actividades programadas de las que se pueden exportar. Acota el periodo o los filtros.',
          );
        } else if (!(error instanceof ApiError) || !EXPLAINED_BY_INTERCEPTOR(error.status)) {
          // Not an HTTP failure (for example the browser could not start the download) or one nobody explained.
          this.notifications.error('No se pudo exportar la agenda.');
        }
        return of(false);
      }),
    );
  }

  private reportScheduling(
    results: readonly ScheduleResult[],
    targets: readonly ActionTarget[],
  ): void {
    const rejected = results.filter((result) => result.error);
    const scheduled = results.length - rejected.length;
    const outlookFailed = results.filter(
      (result) => !result.error && result.outlook.status === 'error',
    );
    const titleOf = (id: string) =>
      targets.find((target) => target.id === id)?.title ?? 'Actividad';

    if (rejected.length > 0) {
      const first = rejected[0];
      this.notifications.error(
        `Se programaron ${scheduled} y ${rejected.length} no se pudieron programar: ${titleOf(first.id)}: ${first.error}`,
      );
    } else if (outlookFailed.length > 0) {
      this.notifications.error(
        `${scheduled === 1 ? 'Actividad programada' : `${scheduled} actividades programadas`}, pero no se pudo publicar en Outlook (${outlookErrorText(outlookFailed[0].outlook.errorCode)}). Usa «Sincronizar de nuevo».`,
      );
    } else {
      this.notifications.info(
        scheduled === 1 ? 'Actividad programada.' : `${scheduled} actividades programadas.`,
      );
    }
  }

  private reportSync(outlook: OutlookState): void {
    if (outlook.status === 'published') {
      this.notifications.info('Actividad publicada en Outlook.');
    } else if (outlook.errorCode === 'not_configured') {
      this.notifications.info('Outlook no configurado: no se publicó nada.');
    } else {
      this.notifications.error(
        `No se pudo publicar en Outlook. ${outlookErrorText(outlook.errorCode)}`.trim(),
      );
    }
  }
}
