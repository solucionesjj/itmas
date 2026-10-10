import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth-service';

/**
 * Route guard driven by `data: { permission: '<resource>.<action>' }`, or a list of permissions that are all required.
 * UX only: the API enforces the same permissions. Users without them are sent to the "access denied" page.
 */
export const permissionGuard: CanActivateFn = (route) => {
  const required = route.data['permission'] as string | readonly string[] | undefined;
  const permissions =
    required === undefined ? [] : typeof required === 'string' ? [required] : required;
  const auth = inject(AuthService);
  return permissions.every((permission) => auth.hasPermission(permission))
    ? true
    : inject(Router).createUrlTree(['/forbidden']);
};
