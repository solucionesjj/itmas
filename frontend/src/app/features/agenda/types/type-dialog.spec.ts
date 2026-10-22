import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { AgendaApi } from '../data/agenda-api';
import { FREEZE_TYPE, PATCHING_TYPE } from '../data/agenda-fixtures';
import { EventType } from '../data/agenda.models';
import { TypeDialog } from './type-dialog';

describe('TypeDialog (RF-1, RF-2, CA-1)', () => {
  const api = { createType: vi.fn(), updateType: vi.fn() };
  const dialogRef = { close: vi.fn() };

  beforeEach(() => {
    api.createType.mockReset().mockImplementation(() => of(PATCHING_TYPE));
    api.updateType.mockReset().mockImplementation(() => of(PATCHING_TYPE));
    dialogRef.close.mockReset();
  });

  const settle = async (fixture: ComponentFixture<unknown>) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const render = async (type: EventType | null) => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AgendaApi, useValue: api },
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: { type } },
      ],
    });
    const fixture = TestBed.createComponent(TypeDialog);
    await settle(fixture);
    return {
      fixture,
      element: fixture.nativeElement as HTMLElement,
      form: fixture.componentInstance['form'],
    };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const submit = async (fixture: ComponentFixture<unknown>, element: HTMLElement) => {
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await settle(fixture);
  };

  describe('creating', () => {
    it('starts with the defaults of the catalog: 60 minutes and a reminder one day before (P-16)', async () => {
      const { element, form } = await render(null);

      expect(text(element.querySelector('h2'))).toBe('Nuevo tipo de actividad');
      expect(form.getRawValue()).toMatchObject({
        defaultDurationMinutes: 60,
        defaultReminderMinutes: 1440,
        isFreeze: false,
      });
      expect(text(element)).not.toContain('Activo (un tipo inactivo');
    });

    it('creates the type with the text trimmed and closes with it', async () => {
      const { fixture, element, form } = await render(null);
      form.patchValue({ name: '  Hardening  ', description: '   ', isFreeze: true });

      await submit(fixture, element);

      expect(api.createType).toHaveBeenCalledWith({
        name: 'Hardening',
        description: null,
        defaultDurationMinutes: 60,
        defaultReminderMinutes: 1440,
        isFreeze: true,
      });
      expect(dialogRef.close).toHaveBeenCalledWith(PATCHING_TYPE);
    });

    it('asks for the name and keeps the figures in range (RF-1)', async () => {
      const { fixture, element, form } = await render(null);
      form.patchValue({ defaultDurationMinutes: 0, defaultReminderMinutes: 40_321 });

      await submit(fixture, element);

      expect(api.createType).not.toHaveBeenCalled();
      expect(text(element)).toContain('El nombre es obligatorio.');
      expect(text(element)).toContain('Un número entero de 1 a 86400.');
      expect(text(element)).toContain('Un número entero de 0 a 40320.');
      expect(dialogRef.close).not.toHaveBeenCalled();
    });

    it('CA-1: shows a repeated name (422) in the dialog without closing it', async () => {
      api.createType.mockReturnValue(
        throwError(() => new ApiError(422, { detail: 'Ya existe un tipo con ese nombre.' })),
      );
      const { fixture, element, form } = await render(null);
      form.patchValue({ name: 'hardening' });

      await submit(fixture, element);

      expect(text(element.querySelector('p.error'))).toBe('Ya existe un tipo con ese nombre.');
      expect(dialogRef.close).not.toHaveBeenCalled();
      expect(element.querySelector<HTMLButtonElement>('button[type="submit"]')!.disabled).toBe(
        false,
      );
    });
  });

  describe('editing', () => {
    it('loads the type, offers to deactivate it and sends the ETag of the revision it read', async () => {
      const type = { ...FREEZE_TYPE, revision: 6 };
      const { fixture, element, form } = await render(type);

      expect(text(element.querySelector('h2'))).toBe('Editar tipo de actividad');
      expect(form.getRawValue()).toMatchObject({ name: type.name, isFreeze: true, isActive: true });
      expect(text(element)).toContain(
        'Activo (un tipo inactivo no se puede asignar a actividades nuevas)',
      );
      form.patchValue({ isActive: false });

      await submit(fixture, element);

      expect(api.updateType).toHaveBeenCalledWith(
        type.id,
        '"6"',
        expect.objectContaining({ name: type.name, isActive: false }),
      );
      expect(api.createType).not.toHaveBeenCalled();
    });

    it('says somebody else changed the type when the version is out of date (412)', async () => {
      api.updateType.mockReturnValue(throwError(() => new ApiError(412, null)));
      const { fixture, element } = await render(PATCHING_TYPE);

      await submit(fixture, element);

      expect(text(element.querySelector('p.error'))).toBe(
        'Otro usuario modificó este tipo. Cierra el diálogo y vuelve a abrirlo para ver los cambios.',
      );
      expect(dialogRef.close).not.toHaveBeenCalled();
    });
  });
});
