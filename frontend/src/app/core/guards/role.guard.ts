import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { UserRole } from '../models/auth.models';

/**
 * Reusable factory instead of one guard class per role (agent.md §5.2 names
 * AdministradorGuard/AuditorGuard/UsuarioGuard) — only instantiate the ones a
 * route actually needs; unused per-role guards would just be dead code.
 * Hiding a nav link for a disallowed role is UX only — the route itself must
 * still carry this guard (defense in depth, never rely on the menu alone).
 */
export function createRoleGuard(...allowedRoles: UserRole[]): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (!authService.isAuthenticated()) {
      return router.createUrlTree(['/login']);
    }

    const user = authService.currentUser();
    if (!user || !allowedRoles.includes(user.role)) {
      return router.createUrlTree(['/']);
    }

    return true;
  };
}

export const administradorGuard = createRoleGuard('administrator');
export const adminOrAuditorGuard = createRoleGuard('administrator', 'auditor');
