import { formatDecimal } from './decimal-format.util';

describe('formatDecimal', () => {
  it('groups thousands with the es-CO separators', () => {
    expect(formatDecimal('1234567.89', 'es-CO')).toBe('1.234.567,89');
  });

  it('groups thousands with the en-US separators', () => {
    expect(formatDecimal('1234567.89', 'en-US')).toBe('1,234,567.89');
  });

  it('keeps every digit of an 18-digit sum — the whole reason it stays a string', () => {
    // Number('1234567890123456.78') is 1234567890123456.8: the cents are gone.
    // Nothing here parses, so they survive.
    expect(formatDecimal('1234567890123456.78', 'en-US')).toBe(
      '1,234,567,890,123,456.78'
    );
  });

  it('pads a missing fractional part to two places', () => {
    expect(formatDecimal('100', 'en-US')).toBe('100.00');
    expect(formatDecimal('100.5', 'en-US')).toBe('100.50');
  });

  it('renders an absent value as an em dash, not as zero', () => {
    // A database that reported no balance is not a database with a balance of 0.
    expect(formatDecimal(null, 'es-CO')).toBe('—');
  });

  it('leaves a value below the grouping threshold alone', () => {
    expect(formatDecimal('999.99', 'es-CO')).toBe('999,99');
  });
});
