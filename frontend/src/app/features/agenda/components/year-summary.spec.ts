import { ComponentFixture, TestBed } from '@angular/core/testing';
import { PATCHING_TYPE, FREEZE_TYPE, summaryTotals } from '../data/agenda-fixtures';
import { Summary } from '../data/agenda.models';
import { YearSummary } from './year-summary';

describe('YearSummary (RF-13, CA-30)', () => {
  const settle = async (fixture: ComponentFixture<unknown>) => {
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  };

  const render = async (summary: Summary | null, month: number | null = null) => {
    const fixture = TestBed.createComponent(YearSummary);
    fixture.componentRef.setInput('summary', summary);
    fixture.componentRef.setInput('year', 2026);
    fixture.componentRef.setInput('month', month);
    fixture.componentRef.setInput('types', [PATCHING_TYPE, FREEZE_TYPE]);
    await settle(fixture);
    return { fixture, element: fixture.nativeElement as HTMLElement };
  };

  const text = (node: Element | null | undefined) =>
    node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';

  const octoberAndNovember = (): Summary => {
    const base = summaryTotals();
    return {
      ...base,
      months: base.months.map((month) =>
        month.month === 10
          ? { ...month, draft: 2, scheduled: 1, overdue: 1 }
          : month.month === 11
            ? { ...month, scheduled: 1, completed: 3, cancelled: 1 }
            : month,
      ),
      byType: [
        { typeId: PATCHING_TYPE.id, count: 6 },
        { typeId: FREEZE_TYPE.id, count: 2 },
      ],
    };
  };

  it('shows the twelve months with how many activities each one has', async () => {
    const { element } = await render(octoberAndNovember());
    const months = [...element.querySelectorAll('mat-chip-option')].map((chip) => text(chip));

    expect(months.length).toBe(12);
    expect(months[0]).toBe('Ene · 0');
    expect(months[9]).toBe('Oct · 3');
    expect(months[10]).toBe('Nov · 5');
  });

  it('gives every month an accessible name with its activities and its overdue ones', async () => {
    const { element } = await render(octoberAndNovember());
    const names = [...element.querySelectorAll('mat-chip-option')].map((chip) =>
      chip.querySelector('button')?.getAttribute('aria-label'),
    );

    expect(names[9]).toBe('Octubre: 3 actividades, 1 vencida');
    expect(names[10]).toBe('Noviembre: 5 actividades');
    expect(names[0]).toBe('Enero: 0 actividades');
  });

  it('totals the year by state, with icon and text, Vencida included', async () => {
    const { element } = await render(octoberAndNovember());
    const [byState] = element.querySelectorAll('mat-card');
    const rows = [...byState.querySelectorAll('li')].map((row) => ({
      icon: text(row.querySelector('mat-icon')),
      label: text(row.querySelector('app-agenda-status span')),
      count: text(row.querySelector('.count')),
    }));

    expect(rows).toEqual([
      { icon: 'edit_note', label: 'Borrador', count: '2' },
      { icon: 'event_available', label: 'Programada', count: '2' },
      { icon: 'check_circle', label: 'Completada', count: '3' },
      { icon: 'cancel', label: 'Cancelada', count: '1' },
      { icon: 'warning', label: 'Vencida', count: '1' },
    ]);
  });

  it('totals the year by type with the name of each type', async () => {
    const { element } = await render(octoberAndNovember());
    const byType = element.querySelectorAll('mat-card')[1];

    expect(
      [...byType.querySelectorAll('li')].map((row) => [
        text(row.querySelector('.type')),
        text(row.querySelector('.count')),
      ]),
    ).toEqual([
      ['Mantenimiento y parches de sistema operativo', '6'],
      ['Congelamiento de cambios', '2'],
    ]);
  });

  it('says there is nothing in a year without activities, and tolerates a summary that has not arrived', async () => {
    const empty = await render(summaryTotals());
    expect(text(empty.element)).toContain('Sin actividades en este año.');

    const loading = await render(null);
    expect(loading.element.querySelectorAll('mat-chip-option').length).toBe(12);
    expect(text(loading.element)).toContain('Sin actividades en este año.');
  });

  it('names a type it does not know instead of failing', async () => {
    const { element } = await render({
      ...summaryTotals(),
      byType: [{ typeId: 'gone', count: 1 }],
    });

    expect(text(element.querySelector('.type'))).toBe('Tipo desconocido');
  });

  it('marks the month the list is narrowed to', async () => {
    const { element } = await render(octoberAndNovember(), 10);
    const selected = [...element.querySelectorAll('mat-chip-option.mat-mdc-chip-selected')];

    expect(selected.map((chip) => text(chip))).toEqual(['Oct · 3']);
  });

  it('tells which month was chosen and null when it is chosen again', async () => {
    const { fixture, element } = await render(octoberAndNovember());
    const changes: (number | null)[] = [];
    fixture.componentInstance.monthChange.subscribe((month) => changes.push(month));
    const chip = element.querySelectorAll('mat-chip-option')[9].querySelector('button')!;

    chip.click();
    await settle(fixture);
    chip.click();
    await settle(fixture);

    expect(changes).toEqual([10, null]);
  });

  it('offers eleven years around the current one', async () => {
    const { fixture } = await render(octoberAndNovember());

    expect(fixture.componentInstance['yearOptions']()).toEqual([
      2021, 2022, 2023, 2024, 2025, 2026, 2027, 2028, 2029, 2030, 2031,
    ]);
  });
});
