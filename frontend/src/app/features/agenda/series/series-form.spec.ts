import { PersonOption } from '../data/agenda.models';
import { createEventForm } from '../events/event-form';
import { createRuleForm, toRuleInput, toSeriesInput } from './series-form';

const ANA: PersonOption = { id: 'u1', name: 'Ana Pérez', isMe: true };

describe('createRuleForm (RF-4, RN-6)', () => {
  it('starts valid: every month, 12 occurrences', () => {
    const rule = createRuleForm();

    expect(rule.valid).toBe(true);
    expect(rule.getRawValue()).toMatchObject({
      unit: 'month',
      interval: 1,
      limitKind: 'count',
      count: 12,
    });
  });

  it('accepts an interval from 1 to 99 and nothing else', () => {
    const rule = createRuleForm();

    for (const interval of [0, 100, 1.5, null]) {
      rule.controls.interval.setValue(interval);
      expect(rule.controls.interval.invalid, String(interval)).toBe(true);
    }
    for (const interval of [1, 90, 99]) {
      rule.controls.interval.setValue(interval);
      expect(rule.controls.interval.valid, String(interval)).toBe(true);
    }
  });

  it('accepts from 1 to 60 occurrences (RN-6)', () => {
    const rule = createRuleForm();

    for (const count of [0, 61, 2.5, null]) {
      rule.controls.count.setValue(count);
      expect(rule.controls.count.invalid, String(count)).toBe(true);
    }
    for (const count of [1, 60]) {
      rule.controls.count.setValue(count);
      expect(rule.controls.count.valid, String(count)).toBe(true);
    }
  });

  it('asks for the final date only while the limit is a date, and for the count only while it is a count', () => {
    const rule = createRuleForm();

    rule.controls.limitKind.setValue('date');
    expect(rule.controls.endsOn.hasError('required')).toBe(true);
    expect(rule.controls.count.valid).toBe(true);

    rule.controls.endsOn.setValue(new Date(2026, 11, 31));
    expect(rule.valid).toBe(true);

    rule.controls.limitKind.setValue('count');
    expect(rule.controls.endsOn.valid).toBe(true);
    rule.controls.count.setValue(null);
    expect(rule.controls.count.hasError('required')).toBe(true);
  });

  it('accepts a day of the month from 1 to 31, or none', () => {
    const rule = createRuleForm();

    rule.controls.dayOfMonth.setValue(32);
    expect(rule.controls.dayOfMonth.invalid).toBe(true);
    rule.controls.dayOfMonth.setValue(31);
    expect(rule.controls.dayOfMonth.valid).toBe(true);
    rule.controls.dayOfMonth.setValue(null);
    expect(rule.controls.dayOfMonth.valid).toBe(true);
  });
});

describe('toRuleInput', () => {
  const start = new Date(2026, 10, 10);

  it('takes the day of the first activity when a monthly series gives no day', () => {
    const rule = createRuleForm();

    expect(toRuleInput(rule, start)).toEqual({
      unit: 'month',
      interval: 1,
      anchor: { kind: 'day-of-month', dayOfMonth: 10 },
      endsOn: null,
      count: 12,
    });
  });

  it('sends a day that does not exist in every month as it is: the API puts it on the last day (RN-6)', () => {
    const rule = createRuleForm();
    rule.patchValue({ dayOfMonth: 31 });

    expect(toRuleInput(rule, start).anchor).toEqual({ kind: 'day-of-month', dayOfMonth: 31 });
  });

  it('sends "the second Tuesday of the month"', () => {
    const rule = createRuleForm();
    rule.patchValue({ anchorKind: 'nth-weekday', nth: 2, weekday: 'tuesday' });

    expect(toRuleInput(rule, start).anchor).toEqual({
      kind: 'nth-weekday',
      nth: 2,
      weekday: 'tuesday',
    });
  });

  it('sends no anchor for days and weeks, whatever the anchor controls hold', () => {
    const rule = createRuleForm();
    rule.patchValue({ unit: 'day', interval: 90, anchorKind: 'nth-weekday' });

    expect(toRuleInput(rule, start)).toMatchObject({ unit: 'day', interval: 90, anchor: null });
    rule.patchValue({ unit: 'week' });
    expect(toRuleInput(rule, start).anchor).toBeNull();
  });

  it('sends a yearly series with its anchor', () => {
    const rule = createRuleForm();
    rule.patchValue({ unit: 'year' });

    expect(toRuleInput(rule, start)).toMatchObject({
      unit: 'year',
      anchor: { kind: 'day-of-month' },
    });
  });

  it('sends the final date or the count, never both', () => {
    const rule = createRuleForm();
    rule.patchValue({ limitKind: 'date', endsOn: new Date(2026, 11, 31) });
    expect(toRuleInput(rule, start)).toMatchObject({ endsOn: '2026-12-31', count: null });

    rule.patchValue({ limitKind: 'count', count: 6 });
    expect(toRuleInput(rule, start)).toMatchObject({ endsOn: null, count: 6 });
  });
});

describe('toSeriesInput', () => {
  it('joins the first activity and the rule', () => {
    const event = createEventForm();
    event.patchValue({
      typeId: 't1',
      title: 'Parches',
      responsible: ANA,
      startDate: new Date(2026, 10, 10),
      startTime: '22:00',
    });

    const input = toSeriesInput(event, createRuleForm());

    expect(input.event).toMatchObject({ title: 'Parches', start: '2026-11-10T22:00' });
    expect(input.rule).toMatchObject({ unit: 'month', interval: 1, count: 12 });
  });
});
