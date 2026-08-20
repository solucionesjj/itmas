/**
 * The locales the portal ships. `es-CO` is the default (design.md §12).
 *
 * These are BCP 47 tags and double as the `LOCALE_ID` Angular's `date`/`number`
 * pipes resolve, so they must match the locale data registered in `app.config.ts`.
 */
export const LOCALES = ['es-CO', 'en-US'] as const;

export type Locale = (typeof LOCALES)[number];

export const DEFAULT_LOCALE: Locale = 'es-CO';

/** Human name of each locale, in its own language — never translated. */
export const LOCALE_NAMES: Record<Locale, string> = {
  'es-CO': 'Español',
  'en-US': 'English'
};

export function isLocale(value: unknown): value is Locale {
  return typeof value === 'string' && (LOCALES as readonly string[]).includes(value);
}
