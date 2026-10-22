import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { NotificationService } from '../../../core/services/notification-service';
import { AgendaApi } from '../data/agenda-api';
import { FREEZE_TYPE, PATCHING_TYPE } from '../data/agenda-fixtures';
import { EventType } from '../data/agenda.models';
import { TypeDialog } from './type-dialog';
import { TypesPage } from './types-page';

describe('TypesPage (RF-1, RF-2, CA-1)', () => {
  let types: EventType[];
  let dialogResult: unknown;
  let confirmation: boolean;
  const api = { listTypes: vi.fn(), updateType: vi.fn() };
  const notifications = { info: vi.fn(), error: vi.fn() };
  const dialog = {
    open: vi.fn((component: unknown) => ({
      afterClosed: () => of(component === TypeDialog ? dialogResult : confirmation),
    })),
  };

  beforeEach(() => {
    types = [PATCHING_TYPE, { ...FREEZE_TYPE, revision: 4 }];
    dialogResult = undefined;
    confirmation = true;
    api.listTypes.mockReset().mockImplementation(() => of(types));
    api.updateType.mockReset().mockImplementation(() => of(PATCHING_TYPE));
    notifications.info.mockReset();
    notifications.error.mockReset();
    dialog.open.mockClear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        { provide: AgendaApi, useValue: api },
        { provide: NotificationService, useValue: notifications },
        { provide: MatDialog, useValue: dialog },
      ],
    });
  });

  const settle = async (fixture: ComponentFixture<unknown>) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const render = async () => {
    const fixture = TestBed.createComponent(TypesPage);
    await settle(fixture);
    return { fixture, element: fixture.nativeElement as HTMLElement };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const rows = (element: HTMLElement) =>
    [...element.querySelectorAll('tr.mat-mdc-row:not(.mat-mdc-no-data-row)')].map((row) =>
      [...row.querySelectorAll('td')].map((cell) => text(cell)),
    );
  const buttonOf = (element: HTMLElement, label: string) =>
    element.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!;

  it('lists each type with its name, defaults, freeze and state, as icon and text', async () => {
    types = [
      { ...PATCHING_TYPE, description: 'Parches mensuales' },
      { ...FREEZE_TYPE, isActive: false },
    ];

    const { element } = await render();
    const [patching, freeze] = rows(element);

    expect(patching[0]).toBe('Mantenimiento y parches de sistema operativo Parches mensuales');
    expect(patching[1]).toBe('1 hora');
    expect(patching[2]).toBe('1 día antes');
    expect(patching[3]).toBe('No');
    expect(patching[4]).toContain('Activo');
    expect(freeze[1]).toBe('1 día');
    expect(freeze[3]).toContain('Congelamiento');
    expect(freeze[4]).toContain('Inactivo');
    expect(element.querySelector('app-agenda-status mat-icon')).not.toBeNull();
  });

  it('says there are no types, and shows the error with a retry', async () => {
    types = [];
    const empty = await render();
    expect(text(empty.element.querySelector('.empty'))).toBe('Todavía no hay tipos de actividad.');

    api.listTypes.mockReturnValueOnce(throwError(() => new ApiError(500, null)));
    const failed = await render();
    expect(text(failed.element.querySelector('[role="alert"]'))).toContain(
      'No se pudieron cargar los tipos de actividad.',
    );
    [...failed.element.querySelectorAll('button')]
      .find((b) => text(b).includes('Reintentar'))!
      .click();
    await settle(failed.fixture);
    expect(rows(failed.element).length).toBe(0);
    expect(api.listTypes).toHaveBeenCalledTimes(3);
  });

  it('opens the dialog to create a type and reloads when it was saved', async () => {
    dialogResult = PATCHING_TYPE;
    const { fixture, element } = await render();
    api.listTypes.mockClear();

    [...element.querySelectorAll('button')].find((b) => text(b).includes('Nuevo tipo'))!.click();
    await settle(fixture);

    expect(dialog.open).toHaveBeenCalledWith(TypeDialog, { data: { type: null } });
    expect(notifications.info).toHaveBeenCalledWith('Tipo creado.');
    expect(api.listTypes).toHaveBeenCalledTimes(1);
  });

  it('opens the dialog with the type to edit, and does nothing when the dialog is dismissed', async () => {
    const { fixture, element } = await render();
    api.listTypes.mockClear();

    buttonOf(element, `Editar el tipo ${PATCHING_TYPE.name}`).click();
    await settle(fixture);

    expect(dialog.open).toHaveBeenCalledWith(TypeDialog, { data: { type: PATCHING_TYPE } });
    expect(notifications.info).not.toHaveBeenCalled();
    expect(api.listTypes).not.toHaveBeenCalled();
  });

  it('RF-2: deactivates a type after a confirmation, sending the rest of it unchanged and its ETag', async () => {
    const { fixture, element } = await render();

    buttonOf(element, `Desactivar el tipo ${FREEZE_TYPE.name}`).click();
    await settle(fixture);

    expect(dialog.open).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({
        data: expect.objectContaining({
          message: expect.stringContaining('ya no se podrá asignar a actividades nuevas'),
        }),
      }),
    );
    expect(api.updateType).toHaveBeenCalledWith(FREEZE_TYPE.id, '"4"', {
      name: FREEZE_TYPE.name,
      description: null,
      defaultDurationMinutes: 1440,
      defaultReminderMinutes: 1440,
      isFreeze: true,
      isActive: false,
    });
    expect(notifications.info).toHaveBeenCalledWith('Tipo desactivado.');
  });

  it('activates a type that was deactivated', async () => {
    types = [{ ...PATCHING_TYPE, isActive: false }];
    const { fixture, element } = await render();

    buttonOf(element, `Activar el tipo ${PATCHING_TYPE.name}`).click();
    await settle(fixture);

    expect(api.updateType).toHaveBeenCalledWith(
      PATCHING_TYPE.id,
      '"1"',
      expect.objectContaining({ isActive: true }),
    );
    expect(notifications.info).toHaveBeenCalledWith('Tipo activado.');
  });

  it('does not change anything when the person does not confirm', async () => {
    confirmation = false;
    const { fixture, element } = await render();

    buttonOf(element, `Desactivar el tipo ${PATCHING_TYPE.name}`).click();
    await settle(fixture);

    expect(api.updateType).not.toHaveBeenCalled();
  });

  it('says somebody else changed the type when the version is out of date (412)', async () => {
    api.updateType.mockReturnValue(throwError(() => new ApiError(412, null)));
    const { fixture, element } = await render();

    buttonOf(element, `Desactivar el tipo ${PATCHING_TYPE.name}`).click();
    await settle(fixture);

    expect(notifications.error).toHaveBeenCalledWith(
      'Otro usuario modificó este tipo. Recarga los datos e inténtalo de nuevo.',
    );
  });

  it('has an accessible name on every icon button', async () => {
    const { element } = await render();

    for (const button of element.querySelectorAll('td button')) {
      expect(button.getAttribute('aria-label'), text(button)).toBeTruthy();
    }
  });
});
