import { INVALID_EMAIL, appendUnique, normalizeEmail, normalizeTag } from './agenda-fields';

describe('normalizeTag (RN-15)', () => {
  it('trims and collapses repeated spaces', () => {
    expect(normalizeTag('  sql   server  ')).toEqual({ value: 'sql server' });
  });

  it('ignores empty text', () => {
    expect(normalizeTag('')).toBeNull();
    expect(normalizeTag('   ')).toBeNull();
  });

  it('accepts exactly 30 characters and rejects 31', () => {
    expect(normalizeTag('a'.repeat(30))).toEqual({ value: 'a'.repeat(30) });
    expect(normalizeTag('a'.repeat(31))).toEqual({
      error: 'Una etiqueta admite como máximo 30 caracteres.',
    });
  });

  it('rejects control characters', () => {
    expect(normalizeTag('a\u0007b')).toEqual({
      error: 'Las etiquetas no pueden contener caracteres de control.',
    });
  });

  it('keeps markup as plain text: it is never interpreted', () => {
    expect(normalizeTag('<b>windows</b>')).toEqual({ value: '<b>windows</b>' });
  });
});

describe('normalizeEmail (RN-5)', () => {
  it('lowercases and trims', () => {
    expect(normalizeEmail('  Ana.Perez@Example.COM ')).toEqual({ value: 'ana.perez@example.com' });
  });

  it('ignores empty text', () => {
    expect(normalizeEmail('  ')).toBeNull();
  });

  it('rejects malformed addresses', () => {
    for (const text of ['ana', 'ana@', '@example.com', 'ana@example', 'a b@example.com']) {
      expect(normalizeEmail(text)).toEqual({ error: INVALID_EMAIL });
    }
  });

  it('rejects what breaks the strict rule of SPEC-0008 RF-2b', () => {
    for (const text of [
      'ana;x@example.com',
      'ana:x@example.com',
      '"ana"@example.com',
      'ana?x@example.com',
      'ana#x@example.com',
      'josé@example.com',
      '.ana@example.com',
      'ana..b@example.com',
      'ana@exa_mple.com',
      `${'a'.repeat(65)}@example.com`,
    ]) {
      expect(normalizeEmail(text), text).toEqual({ error: INVALID_EMAIL });
    }
    expect(normalizeEmail(' Soporte+Agenda@Mail.Example.com ')).toEqual({
      value: 'soporte+agenda@mail.example.com',
    });
  });

  it('rejects addresses longer than 254 characters', () => {
    const long = `${'a'.repeat(60)}@${'d'.repeat(60)}.${'d'.repeat(60)}.${'d'.repeat(60)}.example.com`;
    expect(normalizeEmail(long)).toEqual({ error: INVALID_EMAIL });
  });
});

describe('appendUnique (CA-25)', () => {
  it('adds a new value at the end', () => {
    expect(appendUnique(['windows'], 'producción')).toEqual(['windows', 'producción']);
  });

  it('treats values that differ only in case as the same one and keeps the first spelling', () => {
    expect(appendUnique(['Windows'], 'windows')).toEqual(['Windows']);
  });
});
