import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const authGuard: CanActivateFn = (route) => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (!authService.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }

  const user = authService.currentUser();
  const isChangePasswordRoute = route.routeConfig?.path === 'change-password';

  if (user?.mustChangePassword && !isChangePasswordRoute) {
    return router.createUrlTree(['/change-password']);
  }

  return true;
};
