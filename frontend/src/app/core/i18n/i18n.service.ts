import { Injectable, computed, effect, signal } from '@angular/core';
import { DEFAULT_LOCALE, Locale, isLocale } from './locale';
import { MESSAGES_ES_CO, MessageKey } from './messages.es-CO';
import { MESSAGES_EN_US } from './messages.en-US';

const CATALOGUES: Record<Locale, Record<MessageKey, string>> = {
  'es-CO': MESSAGES_ES_CO,
  'en-US': MESSAGES_EN_US
};

/** Values substituted into a message's `{name}` placeholders. */
export type MessageParams = Readonly<Record<string, string | number>>;

const STORAGE_KEY = 'itmas.locale';

/**
 * Translation lookup, hand-rolled rather than pulled from a library: this is a
 * key→string map with `{name}` interpolation, which does not justify a runtime
 * dependency (agent.md §5.1). If ICU plurals or genders ever become a real
 * requirement, that is the point to migrate — not before.
 *
 * The locale lives in a signal, persisted like the theme, so switching language
 * re-renders in place with no reload and no per-locale build.
 */
@Injectable({ providedIn: 'root' })
export class I18nService {
  readonly locale = signal<Locale>(this.read());

  /** Date/number patterns come from the catalogue: they are locale data (§12). */
  readonly dateTimeFormat = computed(() => this.translate('format.dateTime'));
  readonly dateFormat = computed(() => this.translate('format.date'));

  /**
   * Angular's `DatePipe` takes an offset, not an IANA name, so es-CO carries
   * `-0500` (Colombia, no DST since 1993) and en-US carries none — an en-US
   * reader sees their own zone, per §12.
   */
  readonly timeZone = computed(() => this.translate('format.timeZone') || undefined);

  constructor() {
    effect(() => {
      const locale = this.locale();
      localStorage.setItem(STORAGE_KEY, locale);
      // Keeps the document's language in step, which screen readers and the
      // browser's own hyphenation both rely on.
      document.documentElement.setAttribute('lang', locale);
    });
  }

  set(locale: Locale): void {
    this.locale.set(locale);
  }

  /**
   * Returns the message for `key` in the active locale. A missing key returns the
   * key itself rather than an empty string, so a gap is visible in the UI instead
   * of silently rendering nothing — the catalogues are typed, so this should only
   * ever be reachable through a cast.
   */
  translate(key: MessageKey, params?: MessageParams): string {
    const message = CATALOGUES[this.locale()][key] ?? key;
    if (!params) {
      return message;
    }
    return message.replace(/\{(\w+)\}/g, (match, name: string) =>
      name in params ? String(params[name]) : match
    );
  }

  /** Reads the stored locale, ignoring anything that is not one we ship. */
  private read(): Locale {
    const stored = localStorage.getItem(STORAGE_KEY);
    return isLocale(stored) ? stored : DEFAULT_LOCALE;
  }
}
