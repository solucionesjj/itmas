import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom, of, throwError } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { FileDownload } from '../../../core/services/file-download';
import { NotificationService } from '../../../core/services/notification-service';
import { AgendaApi, NO_FILTERS } from '../data/agenda-api';
import { versionedOf } from '../data/agenda-fixtures';
import { ScheduleResult } from '../data/agenda.models';
import { AgendaActions } from './agenda-actions';
import { CancelDialog } from './cancel-dialog';
import { CompleteDialog } from './complete-dialog';

const ok = (
  id: string,
  outlook: ScheduleResult['outlook'] = { status: 'published' },
): ScheduleResult => ({
  id,
  status: 'scheduled',
  warnings: [],
  outlook,
  error: null,
});

describe('AgendaActions (RF-9, RF-10, RF-19, §8.4)', () => {
  let actions: AgendaActions;
  /** What the dialog answers when it closes: `true`/`false` for a confirmation, or the result of CancelDialog / CompleteDialog. */
  let answer: unknown;
  const api = {
    schedule: vi.fn(),
    deleteEvent: vi.fn(),
    syncOutlook: vi.fn(),
    syncAllOutlook: vi.fn(),
    exportCalendar: vi.fn(),
  };
  const download = { save: vi.fn() };
  const notifications = { info: vi.fn(), error: vi.fn() };
  const dialog = { open: vi.fn((..._args: unknown[]) => ({ afterClosed: () => of(answer) })) };
  const target = { id: 'e1', title: 'Parches de octubre' };

  beforeEach(() => {
    answer = true;
    api.schedule.mockReset().mockImplementation((ids: string[]) => of(ids.map((id) => ok(id))));
    api.deleteEvent.mockReset().mockReturnValue(of(undefined));
    api.syncOutlook.mockReset().mockReturnValue(of({ status: 'published' }));
    api.syncAllOutlook.mockReset().mockReturnValue(of([]));
    api.exportCalendar.mockReset().mockReturnValue(of(new Blob(['BEGIN:VCALENDAR'])));
    download.save.mockReset();
    notifications.info.mockReset();
    notifications.error.mockReset();
    dialog.open.mockClear();
    TestBed.configureTestingModule({
      providers: [
        { provide: AgendaApi, useValue: api },
        { provide: NotificationService, useValue: notifications },
        { provide: FileDownload, useValue: download },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    actions = TestBed.inject(AgendaActions);
  });

  const confirmationOf = () =>
    (
      dialog.open.mock.calls[0][1] as {
        data: { title: string; message: string; confirmLabel: string };
      }
    ).data;

  describe('schedule', () => {
    it('tells the guests will receive their invitations when Outlook is on (§8.4)', async () => {
      await firstValueFrom(actions.schedule([target], true));

      expect(confirmationOf()).toEqual({
        title: 'Programar actividad',
        message: 'Se enviarán invitaciones de Outlook a los invitados de cada actividad.',
        confirmLabel: 'Programar',
      });
    });

    it('says nothing is sent when Outlook is not configured', async () => {
      await firstValueFrom(actions.schedule([target], false));

      expect(confirmationOf().message).toContain(
        'Outlook no está configurado: no se enviarán invitaciones.',
      );
    });

    it('counts the activities in the title of a batch', async () => {
      await firstValueFrom(actions.schedule([target, { id: 'e2', title: 'x' }], true));

      expect(confirmationOf().title).toBe('Programar 2 actividades');
    });

    it('does not call the API when the person does not confirm', async () => {
      answer = false;
      const emitted: boolean[] = [];

      actions.schedule([target], true).subscribe((value) => emitted.push(value));

      expect(api.schedule).not.toHaveBeenCalled();
      expect(emitted).toEqual([]);
    });

    it('schedules the ids and reports one activity or many', async () => {
      await firstValueFrom(actions.schedule([target], true));
      expect(api.schedule).toHaveBeenCalledWith(['e1']);
      expect(notifications.info).toHaveBeenLastCalledWith('Actividad programada.');

      await firstValueFrom(
        actions.schedule([target, { id: 'e2', title: 'x' }, { id: 'e3', title: 'y' }], true),
      );
      expect(notifications.info).toHaveBeenLastCalledWith('3 actividades programadas.');
    });

    it('CA-7: reports the ones that were rejected without hiding the ones that were scheduled', async () => {
      api.schedule.mockReturnValue(
        of([
          ok('e1'),
          {
            id: 'e2',
            status: null,
            warnings: [],
            outlook: { status: 'not-published' },
            error: 'El inicio ya pasó.',
          },
        ]),
      );

      await firstValueFrom(actions.schedule([target, { id: 'e2', title: 'Parches viejos' }], true));

      expect(notifications.error).toHaveBeenCalledWith(
        'Se programaron 1 y 1 no se pudieron programar: Parches viejos: El inicio ya pasó.',
      );
    });

    it('RF-20: reports that it was scheduled but Outlook failed, and points to "Sincronizar de nuevo"', async () => {
      api.schedule.mockReturnValue(
        of([ok('e1', { status: 'error', errorCode: 'graph_unavailable' })]),
      );

      await firstValueFrom(actions.schedule([target], true));

      expect(notifications.error).toHaveBeenCalledWith(
        'Actividad programada, pero no se pudo publicar en Outlook (Outlook no está disponible.). Usa «Sincronizar de nuevo».',
      );
    });

    it('shows the error of the API and emits false', async () => {
      api.schedule.mockReturnValue(throwError(() => new ApiError(400, { detail: 'Más de 50.' })));

      const changed = await firstValueFrom(actions.schedule([target], true));

      expect(changed).toBe(false);
      expect(notifications.error).toHaveBeenCalledWith('Más de 50.');
    });
  });

  describe('cancel', () => {
    it('opens the dialog of the reason with what it needs', () => {
      answer = undefined;

      actions.cancel(target, true).subscribe();

      expect(dialog.open).toHaveBeenCalledWith(CancelDialog, {
        data: { id: 'e1', title: 'Parches de octubre', outlookEnabled: true },
      });
    });

    it('emits nothing when the person backs out', () => {
      answer = undefined;
      const emitted: unknown[] = [];

      actions.cancel(target, true).subscribe((value) => emitted.push(value));

      expect(emitted).toEqual([]);
      expect(notifications.info).not.toHaveBeenCalled();
    });

    it('emits the new version and says it was cancelled', async () => {
      answer = versionedOf({ status: 'cancelled' });

      const result = await firstValueFrom(actions.cancel(target, true));

      expect(result.event.status).toBe('cancelled');
      expect(notifications.info).toHaveBeenCalledWith('Actividad cancelada.');
    });

    it('says when it could not be cancelled in Outlook (RF-20)', async () => {
      answer = versionedOf({
        status: 'cancelled',
        outlook: { status: 'error', errorCode: 'graph_timeout', statusAt: null, webLink: null },
      });

      await firstValueFrom(actions.cancel(target, true));

      expect(notifications.error).toHaveBeenCalledWith(
        'Actividad cancelada, pero no se pudo cancelar en Outlook. Usa «Sincronizar de nuevo».',
      );
    });
  });

  describe('complete', () => {
    it('opens the dialog with the zone and, to edit, the record already made', () => {
      answer = undefined;
      const completion = {
        result: 'succeeded' as const,
        executedAt: '2026-10-24T14:00:00Z',
        completedAt: '2026-10-24T16:00:00Z',
        completedBy: null,
        notes: null,
        evidenceLinks: [],
      };

      actions.complete(target, 'America/Bogota', completion).subscribe();

      expect(dialog.open).toHaveBeenCalledWith(CompleteDialog, {
        data: { id: 'e1', title: 'Parches de octubre', timeZone: 'America/Bogota', completion },
      });
    });

    it('tells apart recording and editing', async () => {
      answer = versionedOf({ status: 'completed' });
      await firstValueFrom(actions.complete(target, 'America/Bogota', null));
      expect(notifications.info).toHaveBeenLastCalledWith('Ejecución registrada.');

      await firstValueFrom(
        actions.complete(target, 'America/Bogota', {
          result: 'succeeded',
          executedAt: '2026-10-24T14:00:00Z',
          completedAt: '2026-10-24T16:00:00Z',
          completedBy: null,
          notes: null,
          evidenceLinks: [],
        }),
      );
      expect(notifications.info).toHaveBeenLastCalledWith('Registro actualizado.');
    });
  });

  describe('deleteDraft (RF-7)', () => {
    it('asks first and says it cannot be undone', async () => {
      await firstValueFrom(actions.deleteDraft(target));

      expect(confirmationOf()).toEqual({
        title: 'Eliminar borrador',
        message: 'Se eliminará «Parches de octubre». Esta acción no se puede deshacer.',
        confirmLabel: 'Eliminar',
      });
      expect(api.deleteEvent).toHaveBeenCalledWith('e1');
      expect(notifications.info).toHaveBeenCalledWith('Borrador eliminado.');
    });

    it('does not delete when the person does not confirm', () => {
      answer = false;

      actions.deleteDraft(target).subscribe();

      expect(api.deleteEvent).not.toHaveBeenCalled();
    });

    it('shows the error (CA-6: only a draft can be deleted) and emits nothing', () => {
      api.deleteEvent.mockReturnValue(
        throwError(() => new ApiError(422, { detail: 'Solo se puede eliminar un borrador.' })),
      );
      const emitted: boolean[] = [];

      actions.deleteDraft(target).subscribe((value) => emitted.push(value));

      expect(emitted).toEqual([]);
      expect(notifications.error).toHaveBeenCalledWith('Solo se puede eliminar un borrador.');
    });
  });

  describe('synchronizing with Outlook (RF-19)', () => {
    it('says it was published', async () => {
      await firstValueFrom(actions.sync(target));

      expect(notifications.info).toHaveBeenCalledWith('Actividad publicada en Outlook.');
    });

    it('says nothing was published when Outlook is not configured', async () => {
      api.syncOutlook.mockReturnValue(of({ status: 'not-published', errorCode: 'not_configured' }));

      await firstValueFrom(actions.sync(target));

      expect(notifications.info).toHaveBeenCalledWith(
        'Outlook no configurado: no se publicó nada.',
      );
    });

    it('explains the failure with the normalized message, never the raw detail', async () => {
      api.syncOutlook.mockReturnValue(of({ status: 'error', errorCode: 'graph_throttled' }));

      await firstValueFrom(actions.sync(target));

      expect(notifications.error).toHaveBeenCalledWith(
        'No se pudo publicar en Outlook. Outlook pidió esperar: demasiadas solicitudes.',
      );
    });

    it('answers every pending activity at once: nothing to do, all done, or some left', async () => {
      await firstValueFrom(actions.syncAll());
      expect(notifications.info).toHaveBeenLastCalledWith(
        'No hay actividades pendientes de sincronizar.',
      );

      api.syncAllOutlook.mockReturnValue(of([{ id: 'a', outlook: { status: 'published' } }]));
      await firstValueFrom(actions.syncAll());
      expect(notifications.info).toHaveBeenLastCalledWith('Actividad sincronizada.');

      api.syncAllOutlook.mockReturnValue(
        of([
          { id: 'a', outlook: { status: 'published' } },
          { id: 'b', outlook: { status: 'published' } },
        ]),
      );
      await firstValueFrom(actions.syncAll());
      expect(notifications.info).toHaveBeenLastCalledWith('2 actividades sincronizadas.');

      api.syncAllOutlook.mockReturnValue(
        of([
          { id: 'a', outlook: { status: 'published' } },
          { id: 'b', outlook: { status: 'pending' } },
          { id: 'c', outlook: { status: 'error' } },
        ]),
      );
      await firstValueFrom(actions.syncAll());
      expect(notifications.error).toHaveBeenLastCalledWith(
        'Se sincronizaron 1 de 3 actividades. Las demás siguen pendientes: vuelve a intentarlo.',
      );
    });
  });

  describe('exportCalendar (RF-21)', () => {
    const target = { from: '2026-01-01', to: '2027-01-01', fileName: 'agenda-2026.ics' };
    const filters = { ...NO_FILTERS, tags: ['windows'], mine: true };

    it('asks the API for the range and the filters and hands the file to the browser with the name of the target', async () => {
      const file = new Blob(['BEGIN:VCALENDAR'], { type: 'text/calendar' });
      api.exportCalendar.mockReturnValue(of(file));

      expect(await firstValueFrom(actions.exportCalendar(target, filters))).toBe(true);

      expect(api.exportCalendar).toHaveBeenCalledWith('2026-01-01', '2027-01-01', filters);
      expect(download.save).toHaveBeenCalledExactlyOnceWith(file, 'agenda-2026.ics');
    });

    it('says the file was downloaded and that it only has the scheduled activities', async () => {
      await firstValueFrom(actions.exportCalendar(target, NO_FILTERS));

      expect(notifications.info).toHaveBeenCalledExactlyOnceWith(
        'Se descargó «agenda-2026.ics». Incluye solo las actividades programadas.',
      );
      expect(notifications.error).not.toHaveBeenCalled();
    });

    it('advises to narrow the period or the filters when there are more activities than the API exports (400)', async () => {
      api.exportCalendar.mockReturnValue(throwError(() => new ApiError(400, null)));

      expect(await firstValueFrom(actions.exportCalendar(target, NO_FILTERS))).toBe(false);

      expect(download.save).not.toHaveBeenCalled();
      expect(notifications.info).not.toHaveBeenCalled();
      expect(notifications.error).toHaveBeenCalledExactlyOnceWith(
        'Hay más actividades programadas de las que se pueden exportar. Acota el periodo o los filtros.',
      );
    });

    it('gives the general message for an error nobody else explains, and downloads nothing', async () => {
      api.exportCalendar.mockReturnValue(throwError(() => new ApiError(404, null)));

      expect(await firstValueFrom(actions.exportCalendar(target, NO_FILTERS))).toBe(false);

      expect(download.save).not.toHaveBeenCalled();
      expect(notifications.error).toHaveBeenCalledExactlyOnceWith('No se pudo exportar la agenda.');
    });

    it.each([0, 403, 429, 500, 503])(
      'leaves the explanation of a %i to the HTTP interceptor and still emits false',
      async (status) => {
        api.exportCalendar.mockReturnValue(throwError(() => new ApiError(status, null)));

        expect(await firstValueFrom(actions.exportCalendar(target, NO_FILTERS))).toBe(false);

        expect(download.save).not.toHaveBeenCalled();
        expect(notifications.error).not.toHaveBeenCalled();
      },
    );

    it('tells the person when the failure is not an HTTP one: nobody else would', async () => {
      api.exportCalendar.mockReturnValue(throwError(() => new Error('boom')));

      expect(await firstValueFrom(actions.exportCalendar(target, NO_FILTERS))).toBe(false);

      expect(download.save).not.toHaveBeenCalled();
      expect(notifications.error).toHaveBeenCalledExactlyOnceWith('No se pudo exportar la agenda.');
    });

    it('tells the person when the browser could not start the download, and does not say it was downloaded', async () => {
      download.save.mockImplementation(() => {
        throw new Error('blocked');
      });

      expect(await firstValueFrom(actions.exportCalendar(target, NO_FILTERS))).toBe(false);

      expect(notifications.info).not.toHaveBeenCalled();
      expect(notifications.error).toHaveBeenCalledExactlyOnceWith('No se pudo exportar la agenda.');
    });
  });
});
