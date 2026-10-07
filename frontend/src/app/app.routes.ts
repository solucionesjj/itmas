import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './auth/guards/auth-guard';

/**
 * Feature areas are lazy loaded. A feature spec adds its routes as a child of the Shell, e.g.
 *   { path: 'users', loadChildren: () => import('./features/users/users.routes') }
 */
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Iniciar sesión',
    loadComponent: () => import('./features/login/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'forbidden',
    title: 'Acceso denegado',
    loadComponent: () => import('./features/errors/forbidden-page').then((m) => m.ForbiddenPage),
  },
  {
    path: '',
    loadComponent: () => import('./layout/shell/shell').then((m) => m.Shell),
    canActivate: [authGuard],
    canActivateChild: [authGuard],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'home' },
      {
        path: 'home',
        title: 'Inicio',
        loadComponent: () => import('./features/home/home-page').then((m) => m.HomePage),
      },
      {
        path: 'admin',
        loadChildren: () => import('./features/authorization/authorization.routes'),
      },
    ],
  },
  {
    path: '**',
    title: 'Página no encontrada',
    loadComponent: () => import('./features/errors/not-found-page').then((m) => m.NotFoundPage),
  },
];
