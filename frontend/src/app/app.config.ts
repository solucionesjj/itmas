import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeEnUs from '@angular/common/locales/en';
import localeEsCo from '@angular/common/locales/es-CO';
import {
  ApplicationConfig,
  LOCALE_ID,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection
} from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { TitleStrategy, provideRouter } from '@angular/router';

import { DEFAULT_LOCALE, isLocale } from './core/i18n/locale';
import { ItmasTitleStrategy } from './core/services/itmas-title.strategy';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { routes } from './app.routes';

// Both locales' data is registered up front, because either can be active
// without a reload (design.md §12; the app ships one bundle, not one per locale).
registerLocaleData(localeEsCo);
registerLocaleData(localeEnUs);

/**
 * `LOCALE_ID` is resolved once at bootstrap from the stored preference. It is what
 * `date`/`number` fall back to when a call site does not pass a locale explicitly,
 * so it has to agree with I18nService's initial value — hence the same storage key
 * and the same default.
 */
function storedLocale(): string {
  const stored = localStorage.getItem('itmas.locale');
  return isLocale(stored) ? stored : DEFAULT_LOCALE;
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideAnimationsAsync(),
    provideHttpClient(withInterceptors([authInterceptor])),
    { provide: TitleStrategy, useClass: ItmasTitleStrategy },
    { provide: LOCALE_ID, useFactory: storedLocale }
  ]
};
