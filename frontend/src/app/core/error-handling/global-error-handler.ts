import { ErrorHandler, Injectable, Injector, inject } from '@angular/core';
import { ApiError } from '../models/api-error';
import { NotificationService } from '../services/notification-service';

/**
 * A notice of the browser, not a failure: the layout changed while a `ResizeObserver` was reporting, so a notification was left for the
 * next frame. Nothing is lost, but the browser raises it as a window error. Calendars (FullCalendar, SPEC-0006) trigger it.
 */
const RESIZE_OBSERVER_NOTICE =
  /ResizeObserver loop (completed with undelivered notifications|limit exceeded)/;

function isResizeObserverNotice(error: unknown): boolean {
  if (!(error instanceof Error)) {
    return false;
  }
  // An ErrorEvent without an error is wrapped by Angular, which keeps the event as the cause.
  const cause = error.cause instanceof ErrorEvent ? error.cause.message : '';
  return RESIZE_OBSERVER_NOTICE.test(`${error.message} ${cause}`);
}

/**
 * Last-resort handler for uncaught errors. HTTP errors were already reported by the HTTP interceptor.
 * Hook point for a future client-side telemetry integration (never send personal data or tokens).
 */
@Injectable()
export class GlobalErrorHandler implements ErrorHandler {
  private readonly injector = inject(Injector);

  handleError(error: unknown): void {
    if (error instanceof ApiError || isResizeObserverNotice(error)) {
      return;
    }

    console.error(error);
    this.injector.get(NotificationService).error('Se produjo un error inesperado.');
  }
}
