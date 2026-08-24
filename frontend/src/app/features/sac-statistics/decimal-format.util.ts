/**
 * Formats a decimal string for display **without ever parsing it into a
 * number**.
 *
 * The API sends the financial sums as strings precisely because a
 * numeric(18,2) does not survive a JavaScript double; running them through
 * `Number()` or Angular's `DecimalPipe` here would silently reintroduce the
 * rounding the whole Decimal128 chain exists to prevent. So the grouping is
 * done on the digit string itself, and the locale only decides which two
 * separators are used.
 */
export function formatDecimal(value: string | null, locale: string): string {
  if (value === null) {
    return '—';
  }
  const [integer, fraction = ''] = value.split('.');
  const { group, decimal } = separatorsFor(locale);

  const grouped = integer.replace(/\B(?=(\d{3})+(?!\d))/g, group);
  const cents = fraction.padEnd(2, '0').slice(0, 2);
  return `${grouped}${decimal}${cents}`;
}

/**
 * Derived from the locale rather than hard-coded per language: es-CO uses
 * `1.234,56` and en-US `1,234.56`, and asking Intl keeps that knowledge in one
 * place even as locales are added.
 */
function separatorsFor(locale: string): { group: string; decimal: string } {
  const parts = new Intl.NumberFormat(locale, {
    minimumFractionDigits: 2
  }).formatToParts(1234.5);
  return {
    group: parts.find((part) => part.type === 'group')?.value ?? ',',
    decimal: parts.find((part) => part.type === 'decimal')?.value ?? '.'
  };
}
