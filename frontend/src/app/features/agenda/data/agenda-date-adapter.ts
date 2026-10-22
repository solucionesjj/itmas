import { Injectable, Provider } from '@angular/core';
import {
  DateAdapter,
  MAT_DATE_FORMATS,
  MAT_NATIVE_DATE_FORMATS,
  NativeDateAdapter,
} from '@angular/material/core';

/** `d/m/yyyy`, also with `-` or `.` as separators: the way a Spanish speaker writes a date. */
const DAY_FIRST = /^\s*(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})\s*$/;

/**
 * Date adapter of the agenda forms (SPEC-0006 §11, P-22). Two things the native one gets wrong for this audience:
 * - the week starts on Monday whatever the browser says, because `NativeDateAdapter` takes the first day from the locale
 *   and some engines answer Sunday for Spanish;
 * - a typed date is read day first. The field shows "24/11/2026", but the browser reads "24/11/2026" as month 24 and gives up,
 *   so the person could only choose dates with the picker.
 */
@Injectable()
export class MondayFirstDateAdapter extends NativeDateAdapter {
  override getFirstDayOfWeek(): number {
    return 1;
  }

  override parse(value: unknown, parseFormat?: unknown): Date | null {
    if (typeof value === 'string') {
      const match = DAY_FIRST.exec(value);
      if (match) {
        const [day, month, year] = [Number(match[1]), Number(match[2]), Number(match[3])];
        const date = new Date(year, month - 1, day);
        // `new Date(2026, 1, 31)` rolls over to March: a day that does not exist is not a date.
        return date.getFullYear() === year &&
          date.getMonth() === month - 1 &&
          date.getDate() === day
          ? date
          : this.invalid();
      }
    }
    return super.parse(value, parseFormat);
  }
}

/** Providers of the date pickers of the agenda. They are scoped to the agenda routes so the other areas keep the default. */
export function provideAgendaDates(): Provider[] {
  return [
    { provide: DateAdapter, useClass: MondayFirstDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: MAT_NATIVE_DATE_FORMATS },
  ];
}
