export type CsvCell = string | number | null | undefined;

/**
 * Wraps a field in double quotes (doubling any internal quote) whenever it
 * contains a comma, a double quote, or a newline — plain values pass through
 * untouched. Never build a CSV row with a naive `.join(',')` on raw values.
 * Callers stringify anything richer (Date, nested object) before calling —
 * this only accepts values with well-defined, lossless string conversion.
 */
export function escapeCsvField(value: CsvCell): string {
  const str = value === null || value === undefined ? '' : String(value);
  if (/[",\n]/.test(str)) {
    return `"${str.replace(/"/g, '""')}"`;
  }
  return str;
}

export function toCsv(headers: string[], rows: CsvCell[][]): string {
  const lines = [headers.map(escapeCsvField).join(',')];
  for (const row of rows) {
    lines.push(row.map(escapeCsvField).join(','));
  }
  return lines.join('\r\n');
}
