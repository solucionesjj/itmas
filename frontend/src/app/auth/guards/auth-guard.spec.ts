import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { AuthService } from '../services/auth-service';
import { authGuard, guestGuard } from './auth-guard';

describe('auth guards', () => {
  const isAuthenticated = signal(false);

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), { provide: AuthService, useValue: { isAuthenticated } }],
    });
  });

  const run = (guard: typeof authGuard, url = '/home') =>
    TestBed.runInInjectionContext(() =>
      guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot),
    );

  it('sends anonymous users to sign-in, keeping the requested URL', () => {
    isAuthenticated.set(false);

    const result = run(authGuard) as UrlTree;

    expect(TestBed.inject(Router).serializeUrl(result)).toBe('/login?returnUrl=%2Fhome');
  });

  it('lets signed-in users through', () => {
    isAuthenticated.set(true);

    expect(run(authGuard)).toBe(true);
  });

  it('keeps signed-in users away from the sign-in page', () => {
    isAuthenticated.set(true);

    expect(TestBed.inject(Router).serializeUrl(run(guestGuard) as UrlTree)).toBe('/');
  });
});
