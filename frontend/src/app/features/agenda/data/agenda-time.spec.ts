import {
  addDays,
  dateOf,
  instantLabel,
  instantToLocalInput,
  isOpenOccurrence,
  isRealDate,
  localToInstant,
  longDate,
  mondayBasedWeekday,
  parseLocal,
  plannedStart,
  shortDate,
  timeOf,
  todayIn,
  whenLabel,
} from './agenda-time';

describe('local dates (RNF-9)', () => {
  it('reads a local date-time and a date without any zone', () => {
    expect(parseLocal('2026-10-24T09:30:00')).toEqual({
      year: 2026,
      month: 10,
      day: 24,
      hour: 9,
      minute: 30,
    });
    expect(parseLocal('2026-10-26')).toEqual({
      year: 2026,
      month: 10,
      day: 26,
      hour: 0,
      minute: 0,
    });
    expect(() => parseLocal('mañana')).toThrow();
  });

  it('shows the time as it comes, never converted to the zone of the browser', () => {
    expect(dateOf('2026-10-24T09:00:00')).toBe('2026-10-24');
    expect(timeOf('2026-10-24T09:00:00')).toBe('09:00');
  });

  it('knows the Monday-based weekday', () => {
    expect(mondayBasedWeekday('2026-10-19')).toBe(0); // Monday
    expect(mondayBasedWeekday('2026-10-24')).toBe(5); // Saturday
    expect(mondayBasedWeekday('2026-10-18')).toBe(6); // Sunday
  });

  it('adds days across months and years', () => {
    expect(addDays('2026-10-31', 1)).toBe('2026-11-01');
    expect(addDays('2026-12-31', 1)).toBe('2027-01-01');
    expect(addDays('2028-03-01', -1)).toBe('2028-02-29');
  });

  it('tells today in the zone of the organization, not in the one of the browser', () => {
    // 03:00 UTC of the 19th is still the evening of the 18th in Bogotá (UTC-5).
    expect(todayIn('America/Bogota', new Date('2026-10-19T03:00:00Z'))).toBe('2026-10-18');
    expect(todayIn('America/Bogota', new Date('2026-10-19T05:00:00Z'))).toBe('2026-10-19');
  });

  describe('plannedStart (decision of 2026-10-24)', () => {
    const zone = 'America/Bogota';
    // 17:22 UTC is 12:22 in Bogotá.
    const noon = new Date('2026-10-23T17:22:00Z');

    it('proposes the current time plus one hour for today, in the zone of the organization', () => {
      expect(plannedStart({ date: '2026-10-23', time: null }, zone, noon)).toEqual({
        date: '2026-10-23',
        time: '13:22',
      });
    });

    it('moves to the next day when the hour after now is past midnight', () => {
      // 04:30 UTC of the 24th is 23:30 of the 23rd in Bogotá.
      expect(
        plannedStart({ date: '2026-10-23', time: null }, zone, new Date('2026-10-24T04:30:00Z')),
      ).toEqual({ date: '2026-10-24', time: '00:30' });
    });

    it('keeps another day or an hour that was chosen', () => {
      expect(plannedStart({ date: '2026-10-27', time: null }, zone, noon)).toEqual({
        date: '2026-10-27',
        time: null,
      });
      expect(plannedStart({ date: '2026-10-23', time: '15:00' }, zone, noon)).toEqual({
        date: '2026-10-23',
        time: '15:00',
      });
    });

    it('decides what today is in the zone of the organization, not in the one of the browser', () => {
      // 03:00 UTC of the 19th: still the 18th (22:00) in Bogotá, already the 19th in the zone of the suite (Asia/Tokyo).
      const lateEvening = new Date('2026-10-19T03:00:00Z');
      expect(plannedStart({ date: '2026-10-18', time: null }, zone, lateEvening)).toEqual({
        date: '2026-10-18',
        time: '23:00',
      });
      expect(plannedStart({ date: '2026-10-19', time: null }, zone, lateEvening)).toEqual({
        date: '2026-10-19',
        time: null,
      });
    });
  });

  it('validates real calendar dates', () => {
    expect(isRealDate('2028-02-29')).toBe(true);
    expect(isRealDate('2026-02-29')).toBe(false);
    expect(isRealDate('2026-13-01')).toBe(false);
    expect(isRealDate('2026-1-1')).toBe(false);
  });
});

describe('labels', () => {
  it('writes dates in Spanish', () => {
    expect(shortDate('2026-10-24')).toBe('sáb 24 oct');
    expect(longDate('2026-12-31')).toBe('31 de diciembre de 2026');
  });

  it('says when a timed activity on one day happens', () => {
    expect(
      whenLabel({ start: '2026-10-24T09:00:00', end: '2026-10-24T11:00:00', isAllDay: false }),
    ).toBe('sáb 24 oct · 09:00–11:00');
  });

  it('says when an activity that crosses days happens, with both days', () => {
    expect(
      whenLabel({ start: '2026-10-24T22:00:00', end: '2026-10-25T02:00:00', isAllDay: false }),
    ).toBe('sáb 24 oct 22:00 → dom 25 oct 02:00');
  });

  it('says when an all-day activity happens: the last day is inclusive (P-24)', () => {
    expect(whenLabel({ start: '2026-10-24', end: '2026-10-24', isAllDay: true })).toBe(
      'sáb 24 oct · todo el día',
    );
    expect(whenLabel({ start: '2026-10-24', end: '2026-10-26', isAllDay: true })).toBe(
      'sáb 24 oct → lun 26 oct · todo el día',
    );
  });
});

describe('instants and the zone of the organization', () => {
  it('converts a local date-time of Bogotá (UTC-5) to its UTC instant and back', () => {
    const instant = localToInstant('2026-10-24T09:00', 'America/Bogota');

    expect(instant).toBe('2026-10-24T14:00:00.000Z');
    expect(instantToLocalInput(instant, 'America/Bogota')).toBe('2026-10-24T09:00');
  });

  it('looks the offset of the zone up for that moment, also across a daylight-saving change', () => {
    // New York: UTC-4 in summer and UTC-5 in winter.
    expect(localToInstant('2026-07-01T09:00', 'America/New_York')).toBe('2026-07-01T13:00:00.000Z');
    expect(localToInstant('2026-12-01T09:00', 'America/New_York')).toBe('2026-12-01T14:00:00.000Z');
  });

  it('shows an instant in the zone of the organization', () => {
    const text = instantLabel('2026-10-24T14:00:00Z', 'America/Bogota');

    expect(text).toContain('2026');
    expect(text).toContain('09:00');
    expect(instantLabel(null, 'America/Bogota')).toBe('');
  });
});

describe('isOpenOccurrence (RF-5, RF-6)', () => {
  const now = Date.parse('2026-10-24T12:00:00Z');

  it('is open while it is a draft or scheduled and has not started', () => {
    expect(isOpenOccurrence({ status: 'draft', startUtc: '2026-10-24T14:00:00Z' }, now)).toBe(true);
    expect(isOpenOccurrence({ status: 'scheduled', startUtc: '2026-10-25T14:00:00Z' }, now)).toBe(
      true,
    );
  });

  it('is not open once it started, or when it is completed or cancelled', () => {
    expect(isOpenOccurrence({ status: 'scheduled', startUtc: '2026-10-24T11:00:00Z' }, now)).toBe(
      false,
    );
    expect(isOpenOccurrence({ status: 'completed', startUtc: '2026-10-25T14:00:00Z' }, now)).toBe(
      false,
    );
    expect(isOpenOccurrence({ status: 'cancelled', startUtc: '2026-10-25T14:00:00Z' }, now)).toBe(
      false,
    );
  });
});
