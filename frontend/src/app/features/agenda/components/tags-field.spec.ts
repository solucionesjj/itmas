import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AgendaApi } from '../data/agenda-api';
import { ChipField } from './chip-field';
import { TagsField } from './tags-field';

describe('TagsField (RF-23, CA-25, §8.4)', () => {
  const api = { listTags: vi.fn() };

  beforeEach(() => {
    api.listTags.mockReset().mockReturnValue(of([]));
    TestBed.configureTestingModule({ providers: [{ provide: AgendaApi, useValue: api }] });
  });

  const settle = async (fixture: ComponentFixture<unknown>) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const render = async (tags: readonly string[] = []) => {
    const fixture = TestBed.createComponent(TagsField);
    fixture.componentRef.setInput('tags', tags);
    await settle(fixture);
    const chipField = fixture.debugElement.children[0].componentInstance as ChipField;
    return { fixture, element: fixture.nativeElement as HTMLElement, chipField };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
  const chips = (element: HTMLElement) =>
    [...element.querySelectorAll('mat-chip-row')].map((c) => text(c));

  it('shows the tags as chips with a button to remove each one', async () => {
    const { element } = await render(['windows', 'producción']);

    expect(chips(element)).toEqual(['windows cancel', 'producción cancel']);
    expect(element.querySelector('button[aria-label="Quitar etiqueta windows"]')).not.toBeNull();
  });

  it('guides the person: "Escribe una etiqueta y pulsa Intro"', async () => {
    const { element } = await render();

    expect(text(element.querySelector('mat-hint'))).toBe('Escribe una etiqueta y pulsa Intro');
  });

  it('adds a tag trimmed and with its spaces collapsed', async () => {
    const { fixture, chipField } = await render();

    chipField.addText('  sql   server ');
    await settle(fixture);

    expect(fixture.componentInstance.tags()).toEqual(['sql server']);
  });

  it('CA-25: "Windows" and "windows" are the same tag and the first spelling is kept', async () => {
    const { fixture, chipField } = await render(['Windows']);

    chipField.addText('windows');
    await settle(fixture);

    expect(fixture.componentInstance.tags()).toEqual(['Windows']);
  });

  it('CA-25: refuses an 11th tag with the message of the specification', async () => {
    const full = Array.from({ length: 10 }, (_, index) => `etiqueta-${index}`);
    const { fixture, element, chipField } = await render(full);

    chipField.addText('una-mas');
    await settle(fixture);

    expect(fixture.componentInstance.tags()).toEqual(full);
    expect(text(element.querySelector('p.error'))).toBe('Máximo 10 etiquetas por actividad.');
  });

  it('CA-25: refuses a tag of more than 30 characters and one with control characters', async () => {
    const { fixture, element, chipField } = await render();

    chipField.addText('a'.repeat(31));
    await settle(fixture);
    expect(text(element.querySelector('p.error'))).toBe(
      'Una etiqueta admite como máximo 30 caracteres.',
    );

    chipField.addText('a\u0001b');
    await settle(fixture);
    expect(text(element.querySelector('p.error'))).toBe(
      'Las etiquetas no pueden contener caracteres de control.',
    );
    expect(fixture.componentInstance.tags()).toEqual([]);
  });

  it('removes a tag with its button', async () => {
    const { fixture, element } = await render(['windows', 'producción']);

    element
      .querySelector<HTMLButtonElement>('button[aria-label="Quitar etiqueta windows"]')!
      .click();
    await settle(fixture);

    expect(fixture.componentInstance.tags()).toEqual(['producción']);
  });

  it('asks for the existing tags as soon as it is shown, so they are reused (RF-23)', async () => {
    await render();
    await new Promise((resolve) => setTimeout(resolve, 300));

    expect(api.listTags).toHaveBeenCalledWith('', 10);
  });

  it('offers the existing tags with how many activities use each one, except the ones already chosen', async () => {
    api.listTags.mockReturnValue(
      of([
        { name: 'windows', count: 12 },
        { name: 'producción', count: 3 },
      ]),
    );
    const { fixture, chipField } = await render(['windows']);
    await new Promise((resolve) => setTimeout(resolve, 300));
    await settle(fixture);

    expect(chipField['suggestions']()).toEqual([
      { value: 'windows', label: 'windows (12)' },
      { value: 'producción', label: 'producción (3)' },
    ]);
    expect(chipField['available']()).toEqual([{ value: 'producción', label: 'producción (3)' }]);
  });

  it('shows a tag with markup as literal text', async () => {
    const { element } = await render(['<b>windows</b>']);

    expect(chips(element)[0]).toContain('<b>windows</b>');
    expect(element.querySelector('mat-chip-row b')).toBeNull();
  });
});

describe('ChipField for participants (RN-5)', () => {
  it('takes the addresses in lowercase and refuses a malformed one', async () => {
    TestBed.configureTestingModule({
      providers: [{ provide: AgendaApi, useValue: { listTags: vi.fn() } }],
    });
    const { normalizeEmail } = await import('../data/agenda-fields');
    const fixture = TestBed.createComponent(ChipField);
    fixture.componentRef.setInput('label', 'Participantes');
    fixture.componentRef.setInput('itemLabel', 'participante');
    fixture.componentRef.setInput('normalize', normalizeEmail);
    fixture.componentRef.setInput('max', 30);
    fixture.componentRef.setInput('maxMessage', 'Máximo 30 participantes por actividad.');
    fixture.detectChanges();

    fixture.componentInstance.addText('Ana@Example.com');
    fixture.componentInstance.addText('ana@example.com');
    fixture.componentInstance.addText('no-es-un-correo');
    fixture.detectChanges();

    expect(fixture.componentInstance.values()).toEqual(['ana@example.com']);
    expect(fixture.nativeElement.querySelector('p.error')?.textContent.trim()).toBe(
      'Introduce un correo válido.',
    );
  });
});
