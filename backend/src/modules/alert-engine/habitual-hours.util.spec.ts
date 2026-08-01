import { isWithinHabitualHours } from './habitual-hours.util';

describe('isWithinHabitualHours', () => {
  it('is within a same-day range (07:00-19:00) at noon UTC', () => {
    const date = new Date('2026-01-05T12:00:00.000Z');
    expect(isWithinHabitualHours(date, 'UTC', '07:00', '19:00')).toBe(true);
  });

  it('is outside a same-day range (07:00-19:00) at 03:00 UTC', () => {
    const date = new Date('2026-01-05T03:00:00.000Z');
    expect(isWithinHabitualHours(date, 'UTC', '07:00', '19:00')).toBe(false);
  });

  it('is exactly on the boundary (inclusive) at 07:00 and 19:00', () => {
    expect(
      isWithinHabitualHours(
        new Date('2026-01-05T07:00:00.000Z'),
        'UTC',
        '07:00',
        '19:00',
      ),
    ).toBe(true);
    expect(
      isWithinHabitualHours(
        new Date('2026-01-05T19:00:00.000Z'),
        'UTC',
        '07:00',
        '19:00',
      ),
    ).toBe(true);
  });

  it('handles an overnight-wrapping range (22:00-06:00) — within at 23:00', () => {
    const date = new Date('2026-01-05T23:00:00.000Z');
    expect(isWithinHabitualHours(date, 'UTC', '22:00', '06:00')).toBe(true);
  });

  it('handles an overnight-wrapping range (22:00-06:00) — within at 02:00', () => {
    const date = new Date('2026-01-05T02:00:00.000Z');
    expect(isWithinHabitualHours(date, 'UTC', '22:00', '06:00')).toBe(true);
  });

  it('handles an overnight-wrapping range (22:00-06:00) — outside at 12:00', () => {
    const date = new Date('2026-01-05T12:00:00.000Z');
    expect(isWithinHabitualHours(date, 'UTC', '22:00', '06:00')).toBe(false);
  });

  it('converts to the configured timezone before comparing', () => {
    // 02:00 UTC == 21:00 previous day in America/New_York (UTC-5 in January)
    // — outside 07:00-19:00 in that timezone even though it'd be within if
    // read as raw UTC hours (which it isn't, since 02:00 is also outside).
    // Use a clearer case: 22:00 UTC == 17:00 in America/New_York (within).
    const date = new Date('2026-01-05T22:00:00.000Z');
    expect(isWithinHabitualHours(date, 'UTC', '07:00', '19:00')).toBe(false);
    expect(
      isWithinHabitualHours(date, 'America/New_York', '07:00', '19:00'),
    ).toBe(true);
  });
});
