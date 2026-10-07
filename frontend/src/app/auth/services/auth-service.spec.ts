import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { BrowserNavigation } from '../../core/services/browser-navigation';
import { AuthService, isLocalPath } from './auth-service';

describe('AuthService', () => {
  let service: AuthService;
  let controller: HttpTestingController;
  const navigation = { assign: vi.fn(), postForm: vi.fn() };

  beforeEach(() => {
    navigation.assign.mockReset();
    navigation.postForm.mockReset();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: BrowserNavigation, useValue: navigation },
      ],
    });
    service = TestBed.inject(AuthService);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('loads the session from the BFF', async () => {
    const loading = service.loadSession();
    controller.expectOne('/auth/session').flush({
      isAuthenticated: true,
      user: { displayName: 'Ada', username: 'ada@example.com' },
    });
    await loading;

    expect(service.isAuthenticated()).toBe(true);
    expect(service.user()?.displayName).toBe('Ada');
  });

  it('exposes permissions for the UI; Root holds every permission', async () => {
    let loading = service.loadSession();
    controller.expectOne('/auth/session').flush({
      isAuthenticated: true,
      user: { displayName: 'Ada', username: null },
      isRoot: false,
      permissions: ['users.read'],
    });
    await loading;
    expect(service.hasPermission('users.read')).toBe(true);
    expect(service.hasPermission('users.update')).toBe(false);

    loading = service.loadSession();
    controller.expectOne('/auth/session').flush({
      isAuthenticated: true,
      user: { displayName: 'Root', username: null },
      isRoot: true,
      permissions: [],
    });
    await loading;
    expect(service.isRoot()).toBe(true);
    expect(service.hasPermission('anything.at.all')).toBe(true);
  });

  it('stays anonymous when the session cannot be loaded', async () => {
    const loading = service.loadSession();
    controller.expectOne('/auth/session').flush(null, { status: 503, statusText: 'Unavailable' });
    await loading;

    expect(service.isAuthenticated()).toBe(false);
  });

  it('starts sign-in through the BFF with a local return URL only', () => {
    service.login('/home?tab=1');
    service.login('https://evil.example.com');

    expect(navigation.assign).toHaveBeenNthCalledWith(1, '/auth/login?returnUrl=%2Fhome%3Ftab%3D1');
    expect(navigation.assign).toHaveBeenNthCalledWith(2, '/auth/login?returnUrl=%2F');
  });

  it('signs out with a form post', () => {
    service.logout();

    expect(navigation.postForm).toHaveBeenCalledWith('/auth/logout');
  });

  it('redirects to sign-in when an active session expires', async () => {
    const loading = service.loadSession();
    controller
      .expectOne('/auth/session')
      .flush({ isAuthenticated: true, user: { displayName: 'Ada', username: null } });
    await loading;
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    service.handleSessionExpired();

    expect(service.isAuthenticated()).toBe(false);
    expect(navigate).toHaveBeenCalledWith(
      ['/login'],
      expect.objectContaining({ queryParams: expect.objectContaining({ reason: 'expired' }) }),
    );
  });

  it.each([
    ['/home', true],
    ['//evil.example.com', false],
    ['/\\evil.example.com', false],
    ['https://evil.example.com', false],
    ['', false],
  ])('isLocalPath(%s) is %s', (url, expected) => {
    expect(isLocalPath(url)).toBe(expected);
  });
});
