import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { Observable, catchError, switchMap, throwError } from 'rxjs';
import { AuthTokens } from '../models/auth.models';
import { AuthService } from '../services/auth.service';

const AUTH_ENDPOINTS = ['/auth/login', '/auth/refresh'];

let refreshInFlight: Observable<AuthTokens> | null = null;

function isAuthEndpoint(url: string): boolean {
  return AUTH_ENDPOINTS.some((path) => url.includes(path));
}

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const router = inject(Router);
  const snackBar = inject(MatSnackBar);

  const accessToken = authService.getAccessToken();
  const authorizedReq =
    accessToken && !isAuthEndpoint(req.url)
      ? req.clone({ setHeaders: { Authorization: `Bearer ${accessToken}` } })
      : req;

  return next(authorizedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401 && !isAuthEndpoint(req.url)) {
        if (!refreshInFlight) {
          refreshInFlight = authService.refresh();
        }

        return refreshInFlight.pipe(
          switchMap((tokens) => {
            refreshInFlight = null;
            const retriedReq = req.clone({
              setHeaders: { Authorization: `Bearer ${tokens.accessToken}` }
            });
            return next(retriedReq);
          }),
          catchError((refreshError) => {
            refreshInFlight = null;
            authService.clearSession();
            router.navigate(['/login']);
            return throwError(() => refreshError);
          })
        );
      }

      if (error.status === 403) {
        const message = error.error?.error?.message ?? 'Acceso denegado para tu perfil.';
        snackBar.open(message, 'Cerrar', { duration: 4000 });
      }

      if (error.status === 401 && isAuthEndpoint(req.url)) {
        authService.clearSession();
        router.navigate(['/login']);
      }

      return throwError(() => error);
    })
  );
};
