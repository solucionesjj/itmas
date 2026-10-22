import {
  AbstractControl,
  FormControl,
  FormGroup,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { dateOf, isRealDate, timeOf, toDateString } from '../data/agenda-time';
import {
  EventDetail,
  EventInput,
  Impact,
  LIMITS,
  PersonOption,
  SeriesCommonInput,
} from '../data/agenda.models';

/**
 * The form of an activity (SPEC-0006 RF-3), shared by the event page and the series page. It is a plain typed form group:
 * the field template (`EventFields`) and the conversion to the API contract live apart so both are easy to test.
 */

/** Reminder presets in minutes; `null` means "the default of the type" (RF-3). */
export const REMINDER_PRESETS: readonly { readonly minutes: number; readonly label: string }[] = [
  { minutes: 0, label: 'En el momento del inicio' },
  { minutes: 15, label: '15 minutos antes' },
  { minutes: 30, label: '30 minutos antes' },
  { minutes: 60, label: '1 hora antes' },
  { minutes: 120, label: '2 horas antes' },
  { minutes: 1440, label: '1 día antes' },
  { minutes: 2880, label: '2 días antes' },
  { minutes: 10080, label: '1 semana antes' },
];

export function createEventForm() {
  return new FormGroup(
    {
      typeId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
      title: new FormControl('', {
        nonNullable: true,
        validators: [Validators.required, Validators.maxLength(LIMITS.title)],
      }),
      description: new FormControl('', {
        nonNullable: true,
        validators: [Validators.maxLength(LIMITS.description)],
      }),
      isAllDay: new FormControl(false, { nonNullable: true }),
      startDate: new FormControl<Date | null>(null, { validators: [Validators.required] }),
      startTime: new FormControl('09:00', { nonNullable: true }),
      endDate: new FormControl<Date | null>(null),
      endTime: new FormControl('', { nonNullable: true }),
      /** The chosen person, or the text being typed while searching. */
      responsible: new FormControl<PersonOption | string | null>(null, {
        validators: [Validators.required, mustBeAPerson],
      }),
      participants: new FormControl<readonly string[]>([], { nonNullable: true }),
      notifyTeam: new FormControl(true, { nonNullable: true }),
      impact: new FormControl<Impact>('none', { nonNullable: true }),
      scope: new FormControl('', {
        nonNullable: true,
        validators: [Validators.maxLength(LIMITS.scope)],
      }),
      location: new FormControl('', {
        nonNullable: true,
        validators: [Validators.maxLength(LIMITS.location)],
      }),
      reminderMinutes: new FormControl<number | null>(null),
      tags: new FormControl<readonly string[]>([], { nonNullable: true }),
    },
    { validators: [timingIsConsistent] },
  );
}

export type EventForm = ReturnType<typeof createEventForm>;

/** The responsible has to be picked from the list, not just typed. */
const mustBeAPerson: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const value = control.value as PersonOption | string | null;
  return value === null || value === '' || typeof value === 'object' ? null : { notAPerson: true };
};

/**
 * Cross-field rules of the timing: an all-day event needs its first day; a timed one its start time; and the end, when given,
 * cannot be before the start. The API checks RN-2 and RN-16 as well (422), with messages shown on the form.
 */
const timingIsConsistent: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const value = group.value as {
    isAllDay: boolean;
    startDate: Date | null;
    startTime: string;
    endDate: Date | null;
    endTime: string;
  };
  if (!value.startDate) {
    return null;
  }
  if (!value.isAllDay && !/^\d{2}:\d{2}$/.test(value.startTime)) {
    return { startTimeRequired: true };
  }
  if (value.endDate) {
    const start = value.startDate.getTime();
    const end = value.endDate.getTime();
    if (end < start) {
      return { endBeforeStart: true };
    }
    if (!value.isAllDay && end === start && value.endTime && value.endTime <= value.startTime) {
      return { endBeforeStart: true };
    }
  }
  if (!value.isAllDay && !value.endDate && value.endTime) {
    return { endDateRequired: true };
  }
  return null;
};

export const dateToString = (date: Date): string =>
  toDateString(date.getFullYear(), date.getMonth() + 1, date.getDate());

/** A local `Date` (midnight) from `yyyy-MM-dd`: the value `mat-datepicker` works with. */
export function stringToDate(value: string): Date | null {
  return isRealDate(value)
    ? new Date(Number(value.slice(0, 4)), Number(value.slice(5, 7)) - 1, Number(value.slice(8, 10)))
    : null;
}

/** Converts the form to the body of POST and PUT `/agenda-events`. */
export function toEventInput(form: EventForm): EventInput {
  const value = form.getRawValue();
  const responsible = value.responsible;
  const allDay = value.isAllDay;
  const startDate = value.startDate ? dateToString(value.startDate) : '';
  const endDate = value.endDate ? dateToString(value.endDate) : null;
  return {
    typeId: value.typeId,
    title: value.title.trim(),
    description: value.description.trim() || null,
    responsibleUserId: typeof responsible === 'object' && responsible ? responsible.id : '',
    participants: value.participants,
    notifyTeam: value.notifyTeam,
    impact: value.impact,
    scope: value.scope.trim() || null,
    location: value.location.trim() || null,
    reminderMinutes: value.reminderMinutes,
    tags: value.tags,
    isAllDay: allDay,
    start: allDay ? startDate : `${startDate}T${value.startTime}`,
    end: allDay ? endDate : endDate ? `${endDate}T${value.endTime || value.startTime}` : null,
  };
}

/**
 * Converts the form to the body of PUT `/agenda-series/{id}` (RF-6): the common fields only, with no dates and no "notify the
 * team". The tags replace the ones of the open occurrences (CA-28).
 */
export function toSeriesCommonInput(form: EventForm): SeriesCommonInput {
  const input = toEventInput(form);
  return {
    typeId: input.typeId,
    title: input.title,
    description: input.description,
    responsibleUserId: input.responsibleUserId,
    participants: input.participants,
    impact: input.impact,
    scope: input.scope,
    location: input.location,
    reminderMinutes: input.reminderMinutes,
    tags: input.tags,
  };
}

/** Controls that belong to the dates of an activity and to its notice: a series edit leaves them out (RF-6). */
export function setCommonOnly(form: EventForm, commonOnly: boolean): void {
  for (const control of [
    form.controls.isAllDay,
    form.controls.startDate,
    form.controls.startTime,
    form.controls.endDate,
    form.controls.endTime,
    form.controls.notifyTeam,
  ]) {
    if (commonOnly) {
      control.disable({ emitEvent: false });
    } else {
      control.enable({ emitEvent: false });
    }
  }
  form.updateValueAndValidity();
}

/** Loads an activity into the form. For a timed event the end is shown as given; the type default is only a fallback. */
export function patchEventForm(form: EventForm, detail: EventDetail): void {
  const start = dateOf(detail.start);
  const end = dateOf(detail.end);
  form.reset(
    {
      typeId: detail.type.id,
      title: detail.title,
      description: detail.description ?? '',
      isAllDay: detail.isAllDay,
      startDate: stringToDate(start),
      startTime: detail.isAllDay ? '09:00' : timeOf(detail.start),
      endDate: stringToDate(end),
      endTime: detail.isAllDay ? '' : timeOf(detail.end),
      responsible: { id: detail.responsible.id, name: detail.responsible.name, isMe: false },
      participants: detail.participants,
      notifyTeam: detail.notifyTeam,
      impact: detail.impact,
      scope: detail.scope ?? '',
      location: detail.location ?? '',
      reminderMinutes: detail.reminderMinutes,
      tags: detail.tags,
    },
    { emitEvent: true },
  );
}

/** Text of a person for the autocomplete input. */
export const personName = (person: PersonOption | string | null): string =>
  typeof person === 'object' && person ? person.name : (person ?? '');
