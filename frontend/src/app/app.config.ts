import { registerLocaleData } from '@angular/common';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import localeEs from '@angular/common/locales/es';
import {
  ApplicationConfig,
  ErrorHandler,
  LOCALE_ID,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { MAT_ICON_DEFAULT_OPTIONS } from '@angular/material/icon';
import { TitleStrategy, provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { sessionExpiryInterceptor } from './auth/interceptors/session-expiry-interceptor';
import { AuthService } from './auth/services/auth-service';
import { AppTitleStrategy } from './core/config/app-title-strategy';
import { GlobalErrorHandler } from './core/error-handling/global-error-handler';
import { csrfInterceptor } from './core/interceptors/csrf-interceptor';
import { httpErrorInterceptor } from './core/interceptors/http-error-interceptor';
import { ThemeService } from './core/services/theme-service';

registerLocaleData(localeEs);

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // Interceptors run in order for requests and in reverse order for responses.
    provideHttpClient(
      withFetch(),
      withInterceptors([csrfInterceptor, httpErrorInterceptor, sessionExpiryInterceptor]),
    ),
    provideAppInitializer(() => {
      inject(ThemeService);
      return inject(AuthService).loadSession();
    }),
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
    { provide: TitleStrategy, useClass: AppTitleStrategy },
    { provide: MAT_ICON_DEFAULT_OPTIONS, useValue: { fontSet: 'material-symbols-outlined' } },
    { provide: LOCALE_ID, useValue: 'es' },
  ],
};
