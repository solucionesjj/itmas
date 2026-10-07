import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { APP_CONFIG } from '../../core/config/app-config';
import { AuthService } from '../services/auth-service';

/** A 401 from the API means the server-side session is gone: send the user to sign in again. */
export const sessionExpiryInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const apiBaseUrl = inject(APP_CONFIG).apiBaseUrl;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        request.url.startsWith(apiBaseUrl)
      ) {
        auth.handleSessionExpired();
      }
      return throwError(() => error);
    }),
  );
};
