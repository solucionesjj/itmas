import { InjectionToken } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface AppConfig {
  readonly appName: string;
  /** Base path of the REST API, served from the same origin through the reverse proxy. */
  readonly apiBaseUrl: string;
  /** Base path of the Backend-for-Frontend session endpoints. */
  readonly authBaseUrl: string;
}

export const APP_CONFIG = new InjectionToken<AppConfig>('APP_CONFIG', {
  providedIn: 'root',
  factory: () => ({
    appName: environment.appName,
    apiBaseUrl: environment.apiBaseUrl,
    authBaseUrl: environment.authBaseUrl,
  }),
});
