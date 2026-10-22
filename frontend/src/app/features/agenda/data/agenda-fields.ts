import { LIMITS } from './agenda.models';

/** Result of normalizing a typed chip: the value to add, an error to show, or `null` to ignore it (empty text). */
export type ChipResult = { readonly value: string } | { readonly error: string } | null;

/** Mirrors RN-15: trimmed, repeated spaces collapsed, 1 to 30 characters, no control characters. The API enforces it too. */
export function normalizeTag(raw: string): ChipResult {
  const cleaned = raw.replace(/\s+/g, ' ').trim();
  if (cleaned === '') {
    return null;
  }
  if (/[\u0000-\u001f\u007f]/.test(cleaned)) {
    return { error: 'Las etiquetas no pueden contener caracteres de control.' };
  }
  if (cleaned.length > LIMITS.tag) {
    return { error: `Una etiqueta admite como máximo ${LIMITS.tag} caracteres.` };
  }
  return { value: cleaned };
}

const EMAIL = /^[^@\s]+@[^@\s]+\.[^@\s]+$/;

/** Mirrors RN-5 on the form side: a well-formed address of at most 254 characters, lowercase. The domain is checked by the API. */
export function normalizeEmail(raw: string): ChipResult {
  const cleaned = raw.trim().toLowerCase();
  if (cleaned === '') {
    return null;
  }
  if (cleaned.length > 254 || !EMAIL.test(cleaned)) {
    return { error: 'Introduce un correo válido.' };
  }
  return { value: cleaned };
}

/** Adds a value to a list unless an equal one (ignoring case) is already there: the first spelling is kept (RN-15). */
export function appendUnique(values: readonly string[], value: string): readonly string[] {
  return values.some((existing) => existing.toLowerCase() === value.toLowerCase())
    ? values
    : [...values, value];
}
