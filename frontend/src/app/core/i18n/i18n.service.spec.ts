import { TestBed } from '@angular/core/testing';
import { I18nService } from './i18n.service';
import { LOCALES } from './locale';
import { MESSAGES_ES_CO, MessageKey } from './messages.es-CO';
import { MESSAGES_EN_US } from './messages.en-US';

describe('I18nService', () => {
  const KEY = 'itmas.locale';

  function makeService(): I18nService {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({});
    return TestBed.inject(I18nService);
  }

  beforeEach(() => localStorage.removeItem(KEY));
  afterAll(() => localStorage.removeItem(KEY));

  it('defaults to es-CO', () => {
    expect(makeService().locale()).toBe('es-CO');
  });

  it('restores a stored locale', () => {
    localStorage.setItem(KEY, 'en-US');
    expect(makeService().locale()).toBe('en-US');
  });

  it('ignores a stored value that is not a locale we ship', () => {
    localStorage.setItem(KEY, 'fr-FR');
    expect(makeService().locale()).toBe('es-CO');
  });

  it('translates from the active catalogue and follows a change', () => {
    const service = makeService();
    expect(service.translate('action.retry')).toBe('Reintentar');

    service.set('en-US');
    expect(service.translate('action.retry')).toBe('Try again');
  });

  it('persists the locale and writes it to <html lang>', () => {
    const service = makeService();
    service.set('en-US');
    TestBed.tick();

    expect(localStorage.getItem(KEY)).toBe('en-US');
    expect(document.documentElement.getAttribute('lang')).toBe('en-US');
  });

  it('interpolates named parameters', () => {
    expect(makeService().translate('users.editFor', { user: 'ana' })).toBe('Editar usuario ana');
  });

  it('leaves an unknown placeholder untouched rather than blanking it', () => {
    // Better a visible `{missing}` than a sentence with a hole in it.
    expect(makeService().translate('users.editFor', { other: 'x' })).toContain('{user}');
  });

  it('exposes the locale-specific date pattern and time zone', () => {
    const service = makeService();
    expect(service.dateTimeFormat()).toBe('dd/MM/yyyy HH:mm');
    expect(service.timeZone()).toBe('-0500');

    service.set('en-US');
    expect(service.dateTimeFormat()).toBe('MM/dd/yyyy h:mm a');
    // en-US shows the reader's own zone, so no offset is forced (§12).
    expect(service.timeZone()).toBeUndefined();
  });

  describe('catalogue integrity', () => {
    const keys = Object.keys(MESSAGES_ES_CO) as MessageKey[];

    it('ships the same keys in both locales', () => {
      expect(Object.keys(MESSAGES_EN_US).sort()).toEqual(keys.slice().sort());
    });

    it('has no empty message except the deliberately blank en-US time zone', () => {
      for (const key of keys) {
        expect(MESSAGES_ES_CO[key].length).toBeGreaterThan(0);
        if (key !== 'format.timeZone') {
          expect(MESSAGES_EN_US[key].length)
            .withContext(`en-US ${key} is empty`)
            .toBeGreaterThan(0);
        }
      }
    });

    it('keeps the same placeholders on both sides of every key', () => {
      const placeholders = (value: string) => (value.match(/\{(\w+)\}/g) ?? []).sort();
      for (const key of keys) {
        expect(placeholders(MESSAGES_EN_US[key]))
          .withContext(`placeholders differ for ${key}`)
          .toEqual(placeholders(MESSAGES_ES_CO[key]));
      }
    });

    it('covers every locale we declare', () => {
      expect(LOCALES.length).toBe(2);
    });
  });
});
