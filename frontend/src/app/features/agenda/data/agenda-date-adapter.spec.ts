import { TestBed } from '@angular/core/testing';
import { DateAdapter, MAT_DATE_LOCALE } from '@angular/material/core';
import { MondayFirstDateAdapter, provideAgendaDates } from './agenda-date-adapter';

describe('MondayFirstDateAdapter (P-22, §11)', () => {
  let adapter: DateAdapter<Date>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideAgendaDates(), { provide: MAT_DATE_LOCALE, useValue: 'es' }],
    });
    adapter = TestBed.inject(DateAdapter);
  });

  it('is the adapter the agenda provides', () => {
    expect(adapter).toBeInstanceOf(MondayFirstDateAdapter);
  });

  it('starts the week on Monday', () => {
    expect(adapter.getFirstDayOfWeek()).toBe(1);
  });

  it('reads a typed date day first, as it is shown', () => {
    expect(adapter.parse('24/11/2026', null)).toEqual(new Date(2026, 10, 24));
    expect(adapter.parse('4/1/2027', null)).toEqual(new Date(2027, 0, 4));
    expect(adapter.parse(' 05-03-2026 ', null)).toEqual(new Date(2026, 2, 5));
    expect(adapter.parse('05.03.2026', null)).toEqual(new Date(2026, 2, 5));
  });

  it('does not accept a day that does not exist', () => {
    for (const text of ['31/02/2026', '29/02/2027', '00/10/2026', '10/13/2026', '32/01/2026']) {
      expect(adapter.isValid(adapter.parse(text, null)!), text).toBe(false);
    }
  });

  it('keeps the leap day of a leap year', () => {
    expect(adapter.parse('29/02/2028', null)).toEqual(new Date(2028, 1, 29));
  });

  it('shows a date the same way it reads it', () => {
    const date = new Date(2026, 10, 24);

    expect(
      adapter.parse(
        adapter.format(date, { year: 'numeric', month: '2-digit', day: '2-digit' }),
        null,
      ),
    ).toEqual(date);
  });

  it('leaves what is not a day-first date to the native parser, and empty text as nothing', () => {
    expect(adapter.parse('', null)).toBeNull();
    expect(adapter.parse(null, null)).toBeNull();
    expect(adapter.isValid(adapter.parse('mañana', null)!)).toBe(false);
  });
});
