import { escapeRegex } from './escape-regex.util';

describe('escapeRegex', () => {
  it('leaves plain alphanumeric input untouched', () => {
    expect(escapeRegex('Windows11')).toBe('Windows11');
  });

  it('escapes regex metacharacters so they match literally', () => {
    const input = 'a.*+?^${}()|[]\\b';
    const escaped = escapeRegex(input);
    const regex = new RegExp(`^${escaped}$`);
    expect(regex.test(input)).toBe(true);
  });

  it('neutralizes a wildcard injection attempt (would otherwise match everything)', () => {
    const escaped = escapeRegex('.*');
    const regex = new RegExp(escaped);
    expect(regex.test('anything at all')).toBe(false);
    expect(regex.test('.*')).toBe(true);
  });
});
