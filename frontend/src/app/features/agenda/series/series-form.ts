import {
  AbstractControl,
  FormControl,
  FormGroup,
  ValidationErrors,
  ValidatorFn,
  Validators,
} from '@angular/forms';
import { EventForm, dateToString, toEventInput } from '../events/event-form';
import {
  AnchorKind,
  LIMITS,
  RecurrenceUnit,
  SeriesInput,
  SeriesRuleInput,
  Weekday,
} from '../data/agenda.models';

/** What limits a series: a number of occurrences or a final date (RF-4). */
export type LimitKind = 'count' | 'date';

const integer: ValidatorFn = (control: AbstractControl): ValidationErrors | null =>
  control.value === null || control.value === '' || Number.isInteger(Number(control.value))
    ? null
    : { integer: true };

/** Required only while its limit is the chosen one: the other field stays hidden and may be empty. */
const requiredWhen =
  (limit: LimitKind): ValidatorFn =>
  (control: AbstractControl): ValidationErrors | null => {
    const kind = control.parent?.get('limitKind')?.value as LimitKind | undefined;
    return kind === limit && (control.value === null || control.value === '')
      ? { required: true }
      : null;
  };

/** The repetition rule of a series (SPEC-0006 RF-4, RN-6). Months and years may anchor on a day or on the n-th weekday. */
export function createRuleForm() {
  const form = new FormGroup({
    unit: new FormControl<RecurrenceUnit>('month', { nonNullable: true }),
    interval: new FormControl<number | null>(1, {
      validators: [Validators.required, Validators.min(1), Validators.max(99), integer],
    }),
    anchorKind: new FormControl<AnchorKind>('day-of-month', { nonNullable: true }),
    /** Empty means the day of the first activity (the API takes it from the start date). */
    dayOfMonth: new FormControl<number | null>(null, {
      validators: [Validators.min(1), Validators.max(31), integer],
    }),
    nth: new FormControl(2, { nonNullable: true }),
    weekday: new FormControl<Weekday>('tuesday', { nonNullable: true }),
    limitKind: new FormControl<LimitKind>('count', { nonNullable: true }),
    count: new FormControl<number | null>(12, {
      validators: [
        requiredWhen('count'),
        Validators.min(1),
        Validators.max(LIMITS.maxOccurrences),
        integer,
      ],
    }),
    endsOn: new FormControl<Date | null>(null, { validators: [requiredWhen('date')] }),
  });
  form.controls.limitKind.valueChanges.subscribe(() => {
    form.controls.count.updateValueAndValidity({ emitEvent: false });
    form.controls.endsOn.updateValueAndValidity({ emitEvent: false });
  });
  return form;
}

export type RuleForm = ReturnType<typeof createRuleForm>;

/** The rule as the API takes it. Anchors only exist for months and years. */
export function toRuleInput(rule: RuleForm, startDate: Date | null): SeriesRuleInput {
  const value = rule.getRawValue();
  const monthly = value.unit === 'month' || value.unit === 'year';
  const anchor = !monthly
    ? null
    : value.anchorKind === 'nth-weekday'
      ? { kind: 'nth-weekday' as const, nth: value.nth, weekday: value.weekday }
      : {
          kind: 'day-of-month' as const,
          dayOfMonth: value.dayOfMonth ?? startDate?.getDate() ?? null,
        };
  return {
    unit: value.unit,
    interval: Number(value.interval),
    anchor,
    endsOn: value.limitKind === 'date' && value.endsOn ? dateToString(value.endsOn) : null,
    count: value.limitKind === 'count' && value.count !== null ? Number(value.count) : null,
  };
}

/** Body of POST `/agenda-series` (also with `dryRun`): the first activity and the rule. */
export function toSeriesInput(event: EventForm, rule: RuleForm): SeriesInput {
  return {
    event: toEventInput(event),
    rule: toRuleInput(rule, event.controls.startDate.value),
  };
}
