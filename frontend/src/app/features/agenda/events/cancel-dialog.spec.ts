import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { AgendaApi } from '../data/agenda-api';
import { versionedOf } from '../data/agenda-fixtures';
import { CancelDialog, CancelDialogData } from './cancel-dialog';

describe('CancelDialog (RF-10, RN-8, CA-8, §8.4)', () => {
  const api = { cancel: vi.fn() };
  const dialogRef = { close: vi.fn() };
  const result = versionedOf({ status: 'cancelled', cancelReason: 'Cambio de ventana' });

  beforeEach(() => {
    api.cancel.mockReset().mockImplementation(() => of(result));
    dialogRef.close.mockReset();
  });

  const settle = async (fixture: ComponentFixture<unknown>) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const render = async (data: Partial<CancelDialogData> = {}) => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AgendaApi, useValue: api },
        { provide: MatDialogRef, useValue: dialogRef },
        {
          provide: MAT_DIALOG_DATA,
          useValue: { id: 'e1', title: 'Parches de octubre', outlookEnabled: true, ...data },
        },
      ],
    });
    const fixture = TestBed.createComponent(CancelDialog);
    await settle(fixture);
    return { fixture, element: fixture.nativeElement as HTMLElement };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const type = async (fixture: ComponentFixture<unknown>, element: HTMLElement, reason: string) => {
    const textarea = element.querySelector('textarea')!;
    textarea.value = reason;
    textarea.dispatchEvent(new Event('input'));
    await settle(fixture);
  };
  const submit = (element: HTMLElement) =>
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!;

  it('names the activity and warns that the guests are told when Outlook is on (§8.4)', async () => {
    const { element } = await render();

    expect(text(element.querySelector('h2'))).toBe('Cancelar actividad');
    expect(text(element.querySelector('.lead'))).toBe('Parches de octubre');
    expect(text(element.querySelector('.supporting'))).toBe(
      'Se cancelará la actividad y se avisará a los invitados. Una actividad cancelada no se puede volver a programar.',
    );
  });

  it('does not talk about guests when Outlook is not configured', async () => {
    const { element } = await render({ outlookEnabled: false });

    expect(text(element.querySelector('.supporting'))).not.toContain('invitados');
  });

  it('RN-8: needs a reason, and spaces alone are not one', async () => {
    const { fixture, element } = await render();
    expect(submit(element).disabled).toBe(true);

    await type(fixture, element, '   ');
    submit(element).click();
    await settle(fixture);

    expect(api.cancel).not.toHaveBeenCalled();
    expect(text(element)).toContain('El motivo es obligatorio.');
  });

  it('counts the characters of the reason against the limit of 500', async () => {
    const { fixture, element } = await render();

    await type(fixture, element, 'Cambio de ventana');

    expect(text(element.querySelector('mat-hint'))).toBe('17/500');
    expect(element.querySelector('textarea')?.getAttribute('maxlength')).toBe('500');
  });

  it('cancels with the reason trimmed and closes with the new version of the activity', async () => {
    const { fixture, element } = await render();
    await type(fixture, element, '  Cambio de ventana  ');

    submit(element).click();
    await settle(fixture);

    expect(api.cancel).toHaveBeenCalledWith('e1', 'Cambio de ventana');
    expect(dialogRef.close).toHaveBeenCalledWith(result);
  });

  it('shows a business-rule error (422) in the dialog and lets the person try again', async () => {
    api.cancel.mockReturnValue(
      throwError(
        () => new ApiError(422, { detail: 'Solo se puede cancelar una actividad programada.' }),
      ),
    );
    const { fixture, element } = await render();
    await type(fixture, element, 'Cambio de ventana');

    submit(element).click();
    await settle(fixture);

    expect(text(element.querySelector('p.error'))).toBe(
      'Solo se puede cancelar una actividad programada.',
    );
    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(submit(element).disabled).toBe(false);
  });
});
