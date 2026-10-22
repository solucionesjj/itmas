import { detailOf } from '../data/agenda-fixtures';
import { PersonOption } from '../data/agenda.models';
import {
  EventForm,
  createEventForm,
  dateToString,
  patchEventForm,
  setCommonOnly,
  stringToDate,
  toEventInput,
  toSeriesCommonInput,
} from './event-form';

const ANA: PersonOption = { id: 'u1', name: 'Ana Pérez', isMe: true };

/** A valid timed activity: type, title, responsible and a start. */
function validForm(): EventForm {
  const form = createEventForm();
  form.patchValue({
    typeId: 't1',
    title: 'Parches de octubre',
    responsible: ANA,
    startDate: new Date(2026, 9, 24),
    startTime: '09:00',
  });
  return form;
}

describe('createEventForm (RF-3, RN-1)', () => {
  it('needs a type, a title, a responsible and a start date', () => {
    const form = createEventForm();

    expect(form.invalid).toBe(true);
    expect(form.controls.typeId.hasError('required')).toBe(true);
    expect(form.controls.title.hasError('required')).toBe(true);
    expect(form.controls.responsible.hasError('required')).toBe(true);
    expect(form.controls.startDate.hasError('required')).toBe(true);
    expect(validForm().valid).toBe(true);
  });

  it('limits the title to 120 characters', () => {
    const form = validForm();
    form.controls.title.setValue('a'.repeat(121));

    expect(form.controls.title.hasError('maxlength')).toBe(true);
  });

  it('requires the responsible to be picked from the list, not typed (RN-4)', () => {
    const form = validForm();
    form.controls.responsible.setValue('an');

    expect(form.controls.responsible.hasError('notAPerson')).toBe(true);
    form.controls.responsible.setValue(ANA);
    expect(form.controls.responsible.valid).toBe(true);
  });

  it('rejects an end before the start, on another day or on the same day (RN-2)', () => {
    const form = validForm();

    form.patchValue({ endDate: new Date(2026, 9, 23) });
    expect(form.hasError('endBeforeStart')).toBe(true);

    form.patchValue({ endDate: new Date(2026, 9, 24), endTime: '08:00' });
    expect(form.hasError('endBeforeStart')).toBe(true);

    form.patchValue({ endDate: new Date(2026, 9, 24), endTime: '11:00' });
    expect(form.errors).toBeNull();
  });

  it('needs the start time of a timed activity and the end date when an end time is given', () => {
    const form = validForm();

    form.patchValue({ startTime: '' });
    expect(form.hasError('startTimeRequired')).toBe(true);

    form.patchValue({ startTime: '09:00', endDate: null, endTime: '10:00' });
    expect(form.hasError('endDateRequired')).toBe(true);
  });

  it('does not ask for the start time of an all-day activity', () => {
    const form = validForm();
    form.patchValue({ isAllDay: true, startTime: '' });

    expect(form.errors).toBeNull();
  });
});

describe('toEventInput (RF-3, P-24)', () => {
  it('sends a timed activity with local date-times and no end when none was given', () => {
    const input = toEventInput(validForm());

    expect(input).toMatchObject({
      typeId: 't1',
      title: 'Parches de octubre',
      responsibleUserId: 'u1',
      isAllDay: false,
      start: '2026-10-24T09:00',
      end: null,
      notifyTeam: true,
      impact: 'none',
      reminderMinutes: null,
    });
  });

  it('sends the end of a timed activity, taking the start time when the end has none', () => {
    const form = validForm();
    form.patchValue({ endDate: new Date(2026, 9, 25), endTime: '02:00' });
    expect(toEventInput(form).end).toBe('2026-10-25T02:00');

    form.patchValue({ endTime: '' });
    expect(toEventInput(form).end).toBe('2026-10-25T09:00');
  });

  it('sends an all-day activity as dates, with the last day inclusive', () => {
    const form = validForm();
    form.patchValue({ isAllDay: true, endDate: new Date(2026, 9, 26) });

    expect(toEventInput(form)).toMatchObject({
      isAllDay: true,
      start: '2026-10-24',
      end: '2026-10-26',
    });
  });

  it('trims the text and sends null for what is empty', () => {
    const form = validForm();
    form.patchValue({ title: '  Parches  ', description: '   ', scope: '', location: ' Remoto ' });

    const input = toEventInput(form);

    expect(input.title).toBe('Parches');
    expect(input.description).toBeNull();
    expect(input.scope).toBeNull();
    expect(input.location).toBe('Remoto');
  });

  it('carries the participants, the tags, the impact and the reminder', () => {
    const form = validForm();
    form.patchValue({
      participants: ['ana@example.com'],
      tags: ['windows', 'producción'],
      impact: 'outage',
      reminderMinutes: 60,
      notifyTeam: false,
    });

    expect(toEventInput(form)).toMatchObject({
      participants: ['ana@example.com'],
      tags: ['windows', 'producción'],
      impact: 'outage',
      reminderMinutes: 60,
      notifyTeam: false,
    });
  });
});

describe('patchEventForm', () => {
  it('loads a timed activity so that saving it again changes nothing', () => {
    const detail = detailOf({
      title: 'Parches',
      participants: ['ana@example.com'],
      tags: ['windows'],
      impact: 'degradation',
      reminderMinutes: 120,
    });
    const form = createEventForm();

    patchEventForm(form, detail);
    const input = toEventInput(form);

    expect(input).toMatchObject({
      typeId: detail.type.id,
      title: 'Parches',
      description: detail.description,
      responsibleUserId: detail.responsible.id,
      participants: ['ana@example.com'],
      tags: ['windows'],
      impact: 'degradation',
      reminderMinutes: 120,
      isAllDay: false,
      start: '2026-10-24T09:00',
      end: '2026-10-24T11:00',
    });
  });

  it('loads an all-day activity with its last day inclusive', () => {
    const form = createEventForm();

    patchEventForm(form, detailOf({ isAllDay: true, start: '2026-10-24', end: '2026-10-26' }));

    expect(toEventInput(form)).toMatchObject({
      isAllDay: true,
      start: '2026-10-24',
      end: '2026-10-26',
    });
  });
});

describe('series edit (RF-6)', () => {
  it('leaves out the dates and the notice option: the form is valid without a start', () => {
    const form = createEventForm();
    form.patchValue({ typeId: 't1', title: 'Parches', responsible: ANA });
    expect(form.invalid).toBe(true);

    setCommonOnly(form, true);
    expect(form.valid).toBe(true);
    expect(form.controls.startDate.disabled).toBe(true);
    expect(form.controls.notifyTeam.disabled).toBe(true);

    setCommonOnly(form, false);
    expect(form.invalid).toBe(true);
  });

  it('sends the common fields only, with the tags that replace the open occurrences (CA-28)', () => {
    const form = validForm();
    form.patchValue({ tags: ['iso-27001'], impact: 'degradation', notifyTeam: false });

    const input = toSeriesCommonInput(form);

    expect(input).toEqual({
      typeId: 't1',
      title: 'Parches de octubre',
      description: null,
      responsibleUserId: 'u1',
      participants: [],
      impact: 'degradation',
      scope: null,
      location: null,
      reminderMinutes: null,
      tags: ['iso-27001'],
    });
    expect('start' in input).toBe(false);
    expect('notifyTeam' in input).toBe(false);
  });
});

describe('dates of the form', () => {
  it('goes from a local date to a string and back without moving the day', () => {
    expect(dateToString(new Date(2026, 0, 5))).toBe('2026-01-05');
    expect(stringToDate('2026-01-05')?.getDate()).toBe(5);
    expect(stringToDate('2026-02-30')).toBeNull();
  });
});
