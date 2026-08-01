import { escapeCsvField, toCsv } from './csv.util';

describe('escapeCsvField', () => {
  it('passes through a plain value unchanged', () => {
    expect(escapeCsvField('Windows')).toBe('Windows');
    expect(escapeCsvField(42)).toBe('42');
  });

  it('renders null/undefined as an empty cell', () => {
    expect(escapeCsvField(null)).toBe('');
    expect(escapeCsvField(undefined)).toBe('');
  });

  it('quotes a value containing a comma', () => {
    expect(escapeCsvField('Ubuntu, 22.04')).toBe('"Ubuntu, 22.04"');
  });

  it('quotes and doubles internal quotes', () => {
    expect(escapeCsvField('12" monitor')).toBe('"12"" monitor"');
  });

  it('quotes a value containing a newline', () => {
    expect(escapeCsvField('line1\nline2')).toBe('"line1\nline2"');
  });
});

describe('toCsv', () => {
  it('builds a header row plus one row per record, CRLF-joined', () => {
    const csv = toCsv(
      ['hostname', 'category'],
      [
        ['PC-001', 'collaborator'],
        ['SRV-001, main', 'infrastructure'],
      ],
    );
    expect(csv).toBe(
      'hostname,category\r\nPC-001,collaborator\r\n"SRV-001, main",infrastructure',
    );
  });
});
