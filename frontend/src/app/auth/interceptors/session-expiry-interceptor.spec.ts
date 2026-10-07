import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../services/auth-service';
import { sessionExpiryInterceptor } from './session-expiry-interceptor';

describe('sessionExpiryInterceptor', () => {
  const auth = { handleSessionExpired: vi.fn() };
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    auth.handleSessionExpired.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([sessionExpiryInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: auth },
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  it('reports an expired session when the API answers 401', () => {
    http.get('/api/v1/things').subscribe({ error: () => undefined });
    controller.expectOne('/api/v1/things').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(auth.handleSessionExpired).toHaveBeenCalledOnce();
  });

  it('ignores 401 from other endpoints and other errors', () => {
    http.get('/auth/session').subscribe({ error: () => undefined });
    http.get('/api/v1/things').subscribe({ error: () => undefined });
    controller.expectOne('/auth/session').flush(null, { status: 401, statusText: 'Unauthorized' });
    controller.expectOne('/api/v1/things').flush(null, { status: 403, statusText: 'Forbidden' });

    expect(auth.handleSessionExpired).not.toHaveBeenCalled();
  });
});
