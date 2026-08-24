import { DECIMAL_18_2, toDecimalString } from './decimal-18-2.util';

describe('toDecimalString', () => {
  it('passes a decimal string through untouched, preserving trailing zeros', () => {
    expect(toDecimalString('1234.50')).toBe('1234.50');
  });

  it('stringifies a JSON number so both wire forms take the same validation path', () => {
    expect(toDecimalString(1234.56)).toBe('1234.56');
  });

  it.each([null, undefined])(
    'leaves %p alone for @IsOptional() to allow',
    (value) => {
      expect(toDecimalString(value)).toBe(value);
    },
  );

  it('leaves a non-finite number as-is so it fails validation instead of becoming "Infinity"-shaped data', () => {
    expect(toDecimalString(Number.POSITIVE_INFINITY)).toBe(
      Number.POSITIVE_INFINITY,
    );
  });
});

describe('DECIMAL_18_2', () => {
  it.each(['0', '1234', '1234.5', '1234.56', '9999999999999999.99'])(
    'accepts %s',
    (value) => {
      expect(DECIMAL_18_2.test(value)).toBe(true);
    },
  );

  it.each([
    ['-1', 'negative — BL-031 rejects negative numerics'],
    ['1234.567', 'three decimal places — the source column is numeric(18,2)'],
    ['12345678901234567', '17 integer digits — beyond numeric(18,2)'],
    ['1e21', 'exponential notation, which a big JS number stringifies to'],
    ['', 'empty'],
    ['12,34', 'comma decimal separator'],
    ['abc', 'not a number'],
  ])('rejects %s (%s)', (value) => {
    expect(DECIMAL_18_2.test(value)).toBe(false);
  });
});
