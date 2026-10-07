import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CSRF_HEADER, csrfInterceptor } from './csrf-interceptor';

describe('csrfInterceptor', () => {
  let http: HttpClient;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([csrfInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('adds the header to same-origin requests', () => {
    http.post('/api/v1/things', {}).subscribe();

    expect(controller.expectOne('/api/v1/things').request.headers.get(CSRF_HEADER)).toBe('1');
  });

  it('never sends the header to other origins', () => {
    http.get('https://example.com/data').subscribe();
    http.get('//example.com/data').subscribe();

    expect(controller.expectOne('https://example.com/data').request.headers.has(CSRF_HEADER)).toBe(
      false,
    );
    expect(controller.expectOne('//example.com/data').request.headers.has(CSRF_HEADER)).toBe(false);
  });
});
