import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ApiError } from '../models/api-error';
import { NotificationService } from '../services/notification-service';
import { httpErrorInterceptor } from './http-error-interceptor';

describe('httpErrorInterceptor', () => {
  const notifications = { error: vi.fn(), info: vi.fn() };
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    notifications.error.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([httpErrorInterceptor])),
        provideHttpClientTesting(),
        { provide: NotificationService, useValue: notifications },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  it('converts errors to ApiError with the Problem Details body', () => {
    let received: unknown;
    http.get('/api/v1/things').subscribe({ error: (error: unknown) => (received = error) });
    controller
      .expectOne('/api/v1/things')
      .flush(
        { title: 'Regla no satisfecha', status: 422, detail: 'detalle' },
        { status: 422, statusText: 'Unprocessable Entity' },
      );

    expect(received).toBeInstanceOf(ApiError);
    expect((received as ApiError).problem?.detail).toBe('detalle');
    expect(notifications.error).not.toHaveBeenCalled();
  });

  it('notifies server errors with the trace reference only', () => {
    http.get('/api/v1/things').subscribe({ error: () => undefined });
    controller
      .expectOne('/api/v1/things')
      .flush(
        { title: 'Error', status: 500, traceId: '00-abc-01' },
        { status: 500, statusText: 'Server Error' },
      );

    expect(notifications.error).toHaveBeenCalledWith(expect.stringContaining('00-abc-01'));
  });
});
