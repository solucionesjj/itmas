import { TestBed } from '@angular/core/testing';
import { ApiError } from '../models/api-error';
import { NotificationService } from '../services/notification-service';
import { GlobalErrorHandler } from './global-error-handler';

describe('GlobalErrorHandler', () => {
  const notifications = { error: vi.fn() };
  let handler: GlobalErrorHandler;

  beforeEach(() => {
    notifications.error.mockReset();
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    TestBed.configureTestingModule({
      providers: [GlobalErrorHandler, { provide: NotificationService, useValue: notifications }],
    });
    handler = TestBed.inject(GlobalErrorHandler);
  });

  afterEach(() => vi.restoreAllMocks());

  it('tells the person about an unexpected error', () => {
    handler.handleError(new Error('boom'));

    expect(notifications.error).toHaveBeenCalledWith('Se produjo un error inesperado.');
  });

  it('does not report again an HTTP error the interceptor already reported', () => {
    handler.handleError(new ApiError(500, null));

    expect(notifications.error).not.toHaveBeenCalled();
  });

  it.each([
    'ResizeObserver loop completed with undelivered notifications.',
    'ResizeObserver loop limit exceeded',
  ])(
    'ignores the browser notice "%s", which is not a failure (calendars trigger it)',
    (message) => {
      const wrapped = new Error(
        `An ErrorEvent with no error occurred. See Error.cause for details: ${message}`,
        {
          cause: new ErrorEvent('error', { message }),
        },
      );

      handler.handleError(wrapped);

      expect(notifications.error).not.toHaveBeenCalled();
    },
  );

  it('still reports any other error event', () => {
    const wrapped = new Error(
      'An ErrorEvent with no error occurred. See Error.cause for details: Script error.',
      {
        cause: new ErrorEvent('error', { message: 'Script error.' }),
      },
    );

    handler.handleError(wrapped);

    expect(notifications.error).toHaveBeenCalled();
  });
});
