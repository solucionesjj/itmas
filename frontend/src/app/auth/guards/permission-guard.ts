import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth-service';

/**
 * Route guard driven by `data: { permission: '<resource>.<action>' }`. UX only: the API enforces the same permission.
 * Users without it are sent to the "access denied" page.
 */
export const permissionGuard: CanActivateFn = (route) => {
  const permission = route.data['permission'] as string | undefined;
  return !permission || inject(AuthService).hasPermission(permission)
    ? true
    : inject(Router).createUrlTree(['/forbidden']);
};
