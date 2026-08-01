function toMinutesSinceMidnight(hhmm: string): number {
  const [hours, minutes] = hhmm.split(':').map(Number);
  return hours * 60 + minutes;
}

/**
 * Whether `date` falls within the `from`-`to` "habitual hours" window, in the
 * given IANA timezone. Handles both same-day ranges (from <= to, e.g.
 * 07:00-19:00) and overnight ranges that wrap past midnight (from > to, e.g.
 * 22:00-06:00 for a night shift).
 */
export function isWithinHabitualHours(
  date: Date,
  timeZone: string,
  from: string,
  to: string,
): boolean {
  const parts = new Intl.DateTimeFormat('en-US', {
    timeZone,
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  }).formatToParts(date);

  const hour = Number(parts.find((part) => part.type === 'hour')?.value ?? 0);
  const minute = Number(
    parts.find((part) => part.type === 'minute')?.value ?? 0,
  );
  const nowMinutes = hour * 60 + minute;

  const fromMinutes = toMinutesSinceMidnight(from);
  const toMinutes = toMinutesSinceMidnight(to);

  if (fromMinutes <= toMinutes) {
    return nowMinutes >= fromMinutes && nowMinutes <= toMinutes;
  }

  // Overnight wrap: habitual if at/after `from` OR at/before `to`.
  return nowMinutes >= fromMinutes || nowMinutes <= toMinutes;
}
