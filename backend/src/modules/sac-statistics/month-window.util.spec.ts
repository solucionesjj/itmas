import { buildMonthWindow } from './month-window.util';

describe('buildMonthWindow', () => {
  it('includes the month `now` falls in as the last entry', () => {
    const { monthKeys } = buildMonthWindow(3, new Date('2026-08-23T12:00:00Z'));

    expect(monthKeys).toEqual(['2026-06', '2026-07', '2026-08']);
  });

  it('walks back across a year boundary', () => {
    const { monthKeys } = buildMonthWindow(4, new Date('2026-02-10T00:00:00Z'));

    expect(monthKeys).toEqual(['2025-11', '2025-12', '2026-01', '2026-02']);
  });

  it('starts the window at midnight UTC on the first of the oldest month', () => {
    const { windowStart } = buildMonthWindow(
      3,
      new Date('2026-08-23T12:00:00Z'),
    );

    expect(windowStart.toISOString()).toBe('2026-06-01T00:00:00.000Z');
  });

  it('handles a single-month window', () => {
    const window = buildMonthWindow(1, new Date('2026-01-31T23:59:59Z'));

    expect(window.monthKeys).toEqual(['2026-01']);
    expect(window.windowStart.toISOString()).toBe('2026-01-01T00:00:00.000Z');
  });

  it('buckets by UTC, not by local time', () => {
    // 01:00 UTC on the 1st is still the previous day in Bogotá (UTC-5); ADR-0018
    // fixes the boundary to UTC so the bucket does not depend on where this runs.
    const { monthKeys } = buildMonthWindow(1, new Date('2026-03-01T01:00:00Z'));

    expect(monthKeys).toEqual(['2026-03']);
  });

  it('pads single-digit months to two characters so keys sort lexicographically', () => {
    const { monthKeys } = buildMonthWindow(
      12,
      new Date('2026-12-15T00:00:00Z'),
    );

    expect(monthKeys).toEqual([...monthKeys].sort());
    expect(monthKeys[0]).toBe('2026-01');
  });

  it('spans a five-year window without gaps or duplicates', () => {
    const { monthKeys } = buildMonthWindow(
      60,
      new Date('2026-08-23T00:00:00Z'),
    );

    expect(monthKeys).toHaveLength(60);
    expect(new Set(monthKeys).size).toBe(60);
    expect(monthKeys[0]).toBe('2021-09');
    expect(monthKeys[59]).toBe('2026-08');
  });
});
