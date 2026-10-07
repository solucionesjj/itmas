import { ErrorHandler, Injectable, Injector, inject } from '@angular/core';
import { ApiError } from '../models/api-error';
import { NotificationService } from '../services/notification-service';

/**
 * Last-resort handler for uncaught errors. HTTP errors were already reported by the HTTP interceptor.
 * Hook point for a future client-side telemetry integration (never send personal data or tokens).
 */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly injector = inject(Injector);

  handleError(error: unknown): void {
    if (error instanceof ApiError) {
      return;
    }

    console.error(error);
    this.injector.get(NotificationService).error('Se produjo un error inesperado.');
  }
}
