import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth-service';

/**
 * Lets only signed-in users reach application routes. UX only: the API enforces authentication
 * and authorisation on every request regardless of what the client does.
 */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isAuthenticated()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

/** Keeps signed-in users away from the sign-in page. */
export const guestGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/']) : true;
