import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { ApiError } from '../models/api-error';
import { NotificationService } from '../services/notification-service';

/**
 * Centralised handling of HTTP errors: converts them into {@link ApiError} and notifies the user of
 * errors that feature code cannot fix (network, permissions, throttling, server). Validation (400/422)
 * and not-found (404) errors are left to the calling feature. 401 is handled by the session interceptor.
 */
export const httpErrorInterceptor: HttpInterceptorFn = (request, next) => {
  const notifications = inject(NotificationService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      const apiError = ApiError.from(error);
      const message = userMessageFor(apiError);
      if (message) {
        notifications.error(message);
      }
      return throwError(() => apiError);
    }),
  );
};

function userMessageFor(error: ApiError): string | null {
  const reference = error.problem?.traceId ? ` (ref. ${error.problem.traceId})` : '';

  if (error.status === 0) {
    return 'No se pudo conectar con el servidor. Comprueba tu conexión.';
  }
  if (error.status === 403) {
    return 'No tienes permisos para realizar esta acción.';
  }
  if (error.status === 429) {
    return 'Demasiadas solicitudes. Espera un momento e inténtalo de nuevo.';
  }
  if (error.status >= 500) {
    return `Se produjo un error en el servidor. Inténtalo más tarde${reference}.`;
  }
  return null;
}
