import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of, throwError } from 'rxjs';
import { ApiError } from '../../../core/models/api-error';
import { AgendaApi } from '../data/agenda-api';
import { versionedOf } from '../data/agenda-fixtures';
import { Completion } from '../data/agenda.models';
import { CompleteDialog, CompleteDialogData } from './complete-dialog';

describe('CompleteDialog (RF-11, RN-9, RN-10, CA-9)', () => {
  const api = { complete: vi.fn() };
  const dialogRef = { close: vi.fn() };
  const result = versionedOf({ status: 'completed' });

  beforeEach(() => {
    api.complete.mockReset().mockImplementation(() => of(result));
    dialogRef.close.mockReset();
  });

  const settle = async (fixture: ComponentFixture<unknown>) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const render = async (data: Partial<CompleteDialogData> = {}) => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AgendaApi, useValue: api },
        { provide: MatDialogRef, useValue: dialogRef },
        {
          provide: MAT_DIALOG_DATA,
          useValue: {
            id: 'e1',
            title: 'Parches de octubre',
            timeZone: 'America/Bogota',
            completion: null,
            ...data,
          },
        },
      ],
    });
    const fixture = TestBed.createComponent(CompleteDialog);
    await settle(fixture);
    return {
      fixture,
      element: fixture.nativeElement as HTMLElement,
      form: fixture.componentInstance['form'],
    };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const button = (element: HTMLElement, label: string) =>
    [...element.querySelectorAll<HTMLButtonElement>('button')].find((b) =>
      text(b).includes(label),
    )!;
  const submit = (element: HTMLElement) =>
    element.querySelector<HTMLButtonElement>('button[type="submit"]')!;

  const completion: Completion = {
    result: 'partial',
    executedAt: '2026-10-24T14:00:00Z',
    completedAt: '2026-10-24T16:00:00Z',
    completedBy: 'Ana Pérez',
    notes: 'Faltó el servidor 4',
    evidenceLinks: [{ label: 'Informe', url: 'https://example.com/informe' }],
  };

  describe('recording the execution', () => {
    it('starts with "Realizada con éxito" and offers the four results (RF-11)', async () => {
      const { fixture, form } = await render();

      expect(text(fixture.nativeElement.querySelector('h2'))).toBe('Registrar ejecución');
      expect(form.controls.result.value).toBe('succeeded');
      expect(fixture.componentInstance['results']).toEqual([
        'succeeded',
        'partial',
        'failed',
        'not-performed',
      ]);
    });

    it('records the result and the notes and closes with the activity as it is now', async () => {
      const { fixture, element, form } = await render();
      form.patchValue({ result: 'failed', notes: '  Falló el reinicio  ' });

      submit(element).click();
      await settle(fixture);

      expect(api.complete).toHaveBeenCalledWith('e1', {
        result: 'failed',
        executedAt: null,
        notes: 'Falló el reinicio',
        evidenceLinks: [],
      });
      expect(dialogRef.close).toHaveBeenCalledWith(result);
    });

    it('turns the real date and time, written in the zone of the organization, into an instant', async () => {
      const { fixture, element, form } = await render();
      form.patchValue({ executedAt: '2026-10-24T09:00' });

      submit(element).click();
      await settle(fixture);

      expect(api.complete.mock.calls[0][1].executedAt).toBe('2026-10-24T14:00:00.000Z');
    });

    it('sends the links trimmed and drops the rows left empty', async () => {
      const { fixture, element, form } = await render();
      button(element, 'Añadir enlace').click();
      button(element, 'Añadir enlace').click();
      await settle(fixture);
      form.controls.evidenceLinks
        .at(0)
        .patchValue({ label: ' Informe ', url: ' https://example.com/i ' });

      submit(element).click();
      await settle(fixture);

      expect(api.complete.mock.calls[0][1].evidenceLinks).toEqual([
        { label: 'Informe', url: 'https://example.com/i' },
      ]);
    });

    it('says evidence is links only, never secrets (§8.4)', async () => {
      const { element } = await render();

      expect(text(element)).toContain(
        'Solo enlaces: no se guardan archivos. No incluyas contraseñas, claves ni hallazgos de seguridad.',
      );
    });
  });

  describe('links of evidence (RN-10)', () => {
    const urlError = async (url: string) => {
      const { fixture, element, form } = await render();
      button(element, 'Añadir enlace').click();
      await settle(fixture);
      form.controls.evidenceLinks.at(0).patchValue({ label: 'x', url });
      return form.controls.evidenceLinks.at(0).controls.url.hasError('pattern');
    };

    it.each([
      'ftp://example.com/archivo',
      'javascript:alert(1)',
      'data:text/html,<script>alert(1)</script>',
      'example.com/sin-esquema',
      'https://usuario:clave@example.com/e',
      'https://usuario@example.com/e',
    ])('refuses %s', async (url) => {
      expect(await urlError(url)).toBe(true);
    });

    it.each([
      'http://example.com',
      'https://example.com/informe?id=3#parte',
      'HTTPS://EXAMPLE.COM/E',
      '  https://example.com/e  ',
    ])('accepts %s', async (url) => {
      expect(await urlError(url)).toBe(false);
    });

    it('shows why a link is refused and does not call the API', async () => {
      const { fixture, element, form } = await render();
      button(element, 'Añadir enlace').click();
      await settle(fixture);
      form.controls.evidenceLinks.at(0).patchValue({ label: 'x', url: 'javascript:alert(1)' });
      form.markAllAsTouched();

      submit(element).click();
      await settle(fixture);

      expect(text(element)).toContain('Debe ser una URL http o https sin usuario ni contraseña.');
      expect(api.complete).not.toHaveBeenCalled();
    });

    it('allows up to 10 links and then disables the button', async () => {
      const { fixture, element } = await render();

      for (let index = 0; index < 12; index++) {
        const add = button(element, 'Añadir enlace');
        if (!add.disabled) {
          add.click();
          await settle(fixture);
        }
      }

      expect(element.querySelectorAll('.link').length).toBe(10);
      expect(button(element, 'Añadir enlace').disabled).toBe(true);
    });

    it('removes a link with its button, which has an accessible name', async () => {
      const { fixture, element, form } = await render({
        completion: undefined,
      });
      button(element, 'Añadir enlace').click();
      await settle(fixture);

      const remove = element.querySelector<HTMLButtonElement>(
        'button[aria-label="Quitar enlace 1"]',
      )!;
      remove.click();
      await settle(fixture);

      expect(form.controls.evidenceLinks.length).toBe(0);
    });
  });

  describe('editing notes and evidence of a completed activity (RN-9)', () => {
    it('loads the record and cannot change the result or the real date', async () => {
      const { element, form } = await render({ completion });

      expect(text(element.querySelector('h2'))).toBe('Editar notas y evidencia');
      expect(form.controls.result.disabled).toBe(true);
      expect(form.controls.result.value).toBe('partial');
      expect(text(element)).toContain('El resultado ya registrado no se puede cambiar.');
      expect(text(element)).not.toContain('Fecha y hora reales de la ejecución');
      expect(form.controls.notes.value).toBe('Faltó el servidor 4');
      expect(form.controls.evidenceLinks.length).toBe(1);
    });

    it('sends the result it has, no date, and the notes and links changed', async () => {
      const { fixture, element, form } = await render({ completion });
      form.patchValue({ notes: 'Se corrigió el servidor 4' });

      submit(element).click();
      await settle(fixture);

      expect(api.complete).toHaveBeenCalledWith('e1', {
        result: 'partial',
        executedAt: null,
        notes: 'Se corrigió el servidor 4',
        evidenceLinks: [{ label: 'Informe', url: 'https://example.com/informe' }],
      });
    });
  });

  it('shows a business-rule error (422) in the dialog without closing it (RN-8)', async () => {
    api.complete.mockReturnValue(
      throwError(
        () => new ApiError(422, { detail: 'Solo se puede registrar desde el inicio programado.' }),
      ),
    );
    const { fixture, element } = await render();

    submit(element).click();
    await settle(fixture);

    expect(text(element.querySelector('p.error'))).toBe(
      'Solo se puede registrar desde el inicio programado.',
    );
    expect(dialogRef.close).not.toHaveBeenCalled();
    expect(submit(element).disabled).toBe(false);
  });
});
