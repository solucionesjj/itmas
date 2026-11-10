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

/**
 * The strict address rule of the agenda (SPEC-0008 RF-2b), the same the API enforces: before the @ only a-z, 0-9 and . _ + -
 * (no dot at the start, at the end or twice), at most 64 characters; after it, ASCII LDH labels.
 */
const EMAIL =
  /^[a-z0-9_+-]+(\.[a-z0-9_+-]+)*@([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?$/;

/** The message of an address that breaks the rule. */
export const INVALID_EMAIL =
  'Introduce un correo válido: antes de la @, solo letras sin tilde, números y . _ + -';

/** Mirrors RN-5 and SPEC-0008 RF-2b on the form side, lowercase. The allowed domains are checked by the API. */
export function normalizeEmail(raw: string): ChipResult {
  const cleaned = raw.trim().toLowerCase();
  if (cleaned === '') {
    return null;
  }
  const at = cleaned.indexOf('@');
  if (cleaned.length > 254 || at > 64 || !EMAIL.test(cleaned)) {
    return { error: INVALID_EMAIL };
  }
  return { value: cleaned };
}

/** Adds a value to a list unless an equal one (ignoring case) is already there: the first spelling is kept (RN-15). */
export function appendUnique(values: readonly string[], value: string): readonly string[] {
  return values.some((existing) => existing.toLowerCase() === value.toLowerCase())
    ? values
    : [...values, value];
}
