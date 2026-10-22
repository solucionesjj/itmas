import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { filter, switchMap } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { problemMessage } from '../../../core/models/problem-message';
import { NotificationService } from '../../../core/services/notification-service';
import { confirm } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { AgendaStatus } from '../components/status-indicator';
import { AgendaApi, etagOf } from '../data/agenda-api';
import { durationLabel, reminderLabel } from '../data/agenda-labels';
import { EventType } from '../data/agenda.models';
import { TypeDialog, TypeDialogData } from './type-dialog';

/**
 * Catalog of activity types (SPEC-0006 RF-1, RF-2, `agenda.types.manage`). A type is never deleted: it is deactivated, and then it
 * cannot be assigned to new activities while the existing ones keep it.
 */
@Component({
  selector: 'app-types-page',
  imports: [
    AgendaStatus,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatTooltipModule,
    RouterLink,
  ],
  templateUrl: './types-page.html',
  styleUrl: '../agenda-page.scss',
})
export class TypesPage {
  private readonly api = inject(AgendaApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly columns = ['name', 'duration', 'reminder', 'freeze', 'status', 'actions'];
  protected readonly types = signal<readonly EventType[]>([]);
  protected readonly loading = signal(true);
  protected readonly failed = signal(false);

  protected readonly durationText = durationLabel;
  protected readonly reminderText = reminderLabel;
  protected readonly active = { icon: 'check_circle', label: 'Activo', tone: 'primary' } as const;
  protected readonly inactive = { icon: 'block', label: 'Inactivo', tone: 'outline' } as const;
  protected readonly freeze = {
    icon: 'ac_unit',
    label: 'Congelamiento',
    tone: 'tertiary',
  } as const;

  constructor() {
    this.load();
  }

  protected load(): void {
    this.loading.set(true);
    this.failed.set(false);
    this.api.listTypes().subscribe({
      next: (types) => {
        this.types.set(types);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      },
    });
  }

  protected create(): void {
    this.openDialog(null);
  }

  protected edit(type: EventType): void {
    this.openDialog(type);
  }

  /** Activating or deactivating asks for a confirmation and sends the rest of the type unchanged (RF-2). */
  protected toggleActive(type: EventType): void {
    const deactivating = type.isActive;
    confirm(this.dialog, {
      title: deactivating ? 'Desactivar tipo' : 'Activar tipo',
      message: deactivating
        ? `«${type.name}» ya no se podrá asignar a actividades nuevas. Las actividades que lo tienen lo conservan.`
        : `«${type.name}» volverá a poder asignarse a actividades nuevas.`,
      confirmLabel: deactivating ? 'Desactivar' : 'Activar',
    })
      .pipe(
        filter((confirmed) => confirmed === true),
        switchMap(() =>
          this.api.updateType(type.id, etagOf(type.revision), {
            name: type.name,
            description: type.description,
            defaultDurationMinutes: type.defaultDurationMinutes,
            defaultReminderMinutes: type.defaultReminderMinutes,
            isFreeze: type.isFreeze,
            isActive: !type.isActive,
          }),
        ),
      )
      .subscribe({
        next: () => {
          this.notifications.info(deactivating ? 'Tipo desactivado.' : 'Tipo activado.');
          this.load();
        },
        error: (error: unknown) =>
          this.notifications.error(
            error instanceof ApiError && error.status === 412
              ? 'Otro usuario modificó este tipo. Recarga los datos e inténtalo de nuevo.'
              : problemMessage(error),
          ),
      });
  }

  private openDialog(type: EventType | null): void {
    this.dialog
      .open<TypeDialog, TypeDialogData, EventType>(TypeDialog, { data: { type } })
      .afterClosed()
      .pipe(filter((saved): saved is EventType => !!saved))
      .subscribe(() => {
        this.notifications.info(type ? 'Tipo actualizado.' : 'Tipo creado.');
        this.load();
      });
  }
}
