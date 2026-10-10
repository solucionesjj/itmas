import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { Observable, of, throwError } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { InventoryApi } from '../data/inventory-api';
import { DeviceInput } from '../data/inventory.models';
import { DeviceFormDialog } from './device-form-dialog';

describe('DeviceFormDialog', () => {
  let create: (input: DeviceInput) => Observable<unknown>;
  let closed: unknown;
  let sent: DeviceInput[];

  beforeEach(() => {
    sent = [];
    closed = undefined;
    create = (input) => {
      sent.push(input);
      return of({ id: 'd1', name: input.name, enrollmentCode: 'ABCDE-FGHJK-MNPQR-STVWX' });
    };
    TestBed.configureTestingModule({
      imports: [DeviceFormDialog],
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: { mode: 'create' } },
        { provide: MatDialogRef, useValue: { close: (value: unknown) => (closed = value) } },
        {
          provide: InventoryApi,
          useValue: { createDevice: (input: DeviceInput) => create(input) },
        },
      ],
    });
  });

  const fill = async (name: string, email = '') => {
    const fixture = TestBed.createComponent(DeviceFormDialog);
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    const [nameInput, emailInput] = [...element.querySelectorAll('input')];
    nameInput.value = name;
    nameInput.dispatchEvent(new Event('input'));
    emailInput.value = email;
    emailInput.dispatchEvent(new Event('input'));
    element.querySelector<HTMLButtonElement>('button[value="server"]')?.click();
    [...element.querySelectorAll('button')]
      .find((button) => button.textContent?.includes('Servidor'))
      ?.click();
    fixture.detectChanges();
    element.querySelector('form')!.dispatchEvent(new Event('submit'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    return element;
  };

  it('creates the device with the normalized input and returns the code to the caller', async () => {
    await fill('  srv-web  ', ' ops@example.com ');

    expect(sent).toEqual([{ name: 'srv-web', type: 'server', ownerEmail: 'ops@example.com' }]);
    expect(closed).toEqual(expect.objectContaining({ enrollmentCode: 'ABCDE-FGHJK-MNPQR-STVWX' }));
  });

  it('sends no e-mail when it is left empty', async () => {
    await fill('pc-ana');

    expect(sent[0].ownerEmail).toBeNull();
  });

  it('shows a duplicate name next to the field and keeps the dialog open', async () => {
    create = () =>
      throwError(
        () =>
          new ApiError(422, {
            status: 422,
            title: 'Regla',
            detail: 'Ya existe un equipo con este nombre.',
          }),
      );

    const element = await fill('pc-ana');

    expect(closed).toBeUndefined();
    expect(element.querySelector('mat-error')?.textContent).toContain(
      'Ya existe un equipo con este nombre.',
    );
  });
});
