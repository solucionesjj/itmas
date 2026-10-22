import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AgendaApi, AgendaFilters, NO_FILTERS } from '../data/agenda-api';
import { TYPES } from '../data/agenda-fixtures';
import { AgendaFiltersBar } from './filters-bar';

describe('AgendaFiltersBar (RF-12, RF-25, CA-26)', () => {
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

  const render = async (filters: AgendaFilters = NO_FILTERS) => {
    const fixture = TestBed.createComponent(AgendaFiltersBar);
    fixture.componentRef.setInput('filters', filters);
    fixture.componentRef.setInput('types', TYPES);
    fixture.componentRef.setInput('people', [
      { id: 'u1', name: 'Ana Pérez' },
      { id: 'u2', name: 'Luis Gómez' },
    ]);
    const emitted: AgendaFilters[] = [];
    fixture.componentInstance.filtersChange.subscribe((value) => emitted.push(value));
    await settle(fixture);
    return { fixture, element: fixture.nativeElement as HTMLElement, emitted };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';

  /** Opens a select by its label and returns the options of its panel. */
  const openSelect = async (
    fixture: ComponentFixture<unknown>,
    element: HTMLElement,
    label: string,
  ) => {
    const field = [...element.querySelectorAll('mat-form-field')].find(
      (f) => text(f.querySelector('mat-label')) === label,
    )!;
    field.querySelector<HTMLElement>('.mat-mdc-select-trigger')!.click();
    await settle(fixture);
    return [...document.querySelectorAll<HTMLElement>('.mat-mdc-option')];
  };

  afterEach(() =>
    document.querySelectorAll('.cdk-overlay-container').forEach((c) => (c.innerHTML = '')),
  );

  it('is a search region with every filter of the specification', async () => {
    const { element } = await render();
    const labels = [...element.querySelectorAll('mat-label')].map((label) => text(label));

    expect(element.querySelector('[role="search"]')?.getAttribute('aria-label')).toBe(
      'Filtros de la agenda',
    );
    expect(labels).toEqual([
      'Buscar por título o etiqueta',
      'Tipo',
      'Estado',
      'Responsable',
      'Filtrar por etiquetas',
    ]);
    expect(text(element.querySelector('mat-slide-toggle'))).toBe('Mis actividades');
  });

  it('says tags combine as "all of them" (P-18)', async () => {
    const { element } = await render();

    expect(text(element.querySelector('app-tags-field mat-hint'))).toBe(
      'Se muestran las actividades que tienen todas las etiquetas.',
    );
  });

  it('offers "Vencidas" among the states (CA-10)', async () => {
    const { fixture, element } = await render();

    const options = (await openSelect(fixture, element, 'Estado')).map((option) => text(option));

    expect(options).toEqual([
      'Todos',
      'Borrador',
      'Programada',
      'Completada',
      'Cancelada',
      'Vencidas',
    ]);
  });

  it('keeps the other filters when one changes', async () => {
    const { fixture, element, emitted } = await render({
      ...NO_FILTERS,
      tags: ['windows'],
      search: 'parches',
    });

    const options = await openSelect(fixture, element, 'Estado');
    options.find((option) => text(option) === 'Vencidas')!.click();
    await settle(fixture);

    expect(emitted.at(-1)).toEqual({
      ...NO_FILTERS,
      tags: ['windows'],
      search: 'parches',
      status: 'overdue',
    });
  });

  it('filters by the type chosen', async () => {
    const { fixture, element, emitted } = await render();

    const options = await openSelect(fixture, element, 'Tipo');
    options.find((option) => text(option) === 'Congelamiento de cambios')!.click();
    await settle(fixture);

    expect(emitted.at(-1)?.type).toBe('t-freeze');
  });

  it('filters by responsible with the people it was given', async () => {
    const { fixture, element, emitted } = await render();

    const options = await openSelect(fixture, element, 'Responsable');
    expect(options.map((option) => text(option))).toEqual(['Todos', 'Ana Pérez', 'Luis Gómez']);
    options.find((option) => text(option) === 'Luis Gómez')!.click();
    await settle(fixture);

    expect(emitted.at(-1)?.responsible).toBe('u2');
  });

  it('filters by "Mis actividades"', async () => {
    const { fixture, element, emitted } = await render();

    element.querySelector<HTMLButtonElement>('mat-slide-toggle button')!.click();
    await settle(fixture);

    expect(emitted.at(-1)?.mine).toBe(true);
  });

  it('searches by title or tag after the person pauses typing, with the text trimmed', async () => {
    const { fixture, element, emitted } = await render();
    const input = element.querySelector<HTMLInputElement>('input[maxlength="100"]')!;

    input.value = '  parches ';
    input.dispatchEvent(new Event('input'));
    expect(emitted).toEqual([]);
    await new Promise((resolve) => setTimeout(resolve, 350));
    await settle(fixture);

    expect(emitted).toEqual([{ ...NO_FILTERS, search: 'parches' }]);
  });

  it('shows "Limpiar filtros" only while a filter is on, and clears every one (CA-34)', async () => {
    const off = await render();
    expect(
      [...off.element.querySelectorAll('button')].some((b) => text(b).includes('Limpiar filtros')),
    ).toBe(false);

    const on = await render({ ...NO_FILTERS, tags: ['windows'], mine: true, status: 'draft' });
    const clear = [...on.element.querySelectorAll('button')].find((b) =>
      text(b).includes('Limpiar filtros'),
    )!;
    clear.click();

    expect(on.emitted.at(-1)).toEqual(NO_FILTERS);
  });

  it('shows the tags of the filter as chips that can be removed', async () => {
    const { fixture, element, emitted } = await render({
      ...NO_FILTERS,
      tags: ['windows', 'iso-27001'],
    });

    expect([...element.querySelectorAll('mat-chip-row')].map((chip) => text(chip))).toEqual([
      'windows cancel',
      'iso-27001 cancel',
    ]);
    element
      .querySelector<HTMLButtonElement>('button[aria-label="Quitar etiqueta windows"]')!
      .click();
    await settle(fixture);

    expect(emitted.at(-1)?.tags).toEqual(['iso-27001']);
  });
});
