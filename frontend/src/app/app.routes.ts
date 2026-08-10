import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { administradorGuard, adminOrAuditorGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'change-password',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./features/change-password/change-password.component').then(
        (m) => m.ChangePasswordComponent
      )
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./core/layout/shell.component').then((m) => m.ShellComponent),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then(
            (m) => m.DashboardComponent
          )
      },
      {
        path: 'devices',
        loadComponent: () =>
          import('./features/devices/devices-list.component').then(
            (m) => m.DevicesListComponent
          )
      },
      {
        path: 'alerts',
        canActivate: [adminOrAuditorGuard],
        loadComponent: () =>
          import('./features/alerts/alerts-list.component').then(
            (m) => m.AlertsListComponent
          )
      },
      {
        path: 'admin/users',
        canActivate: [administradorGuard],
        loadComponent: () =>
          import('./features/admin/users/users-list.component').then(
            (m) => m.UsersListComponent
          )
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./features/reports/reports.component').then(
            (m) => m.ReportsComponent
          )
      },
      {
        path: 'security-group-rules',
        loadComponent: () =>
          import(
            './features/security-group-rules/security-group-rules-list.component'
          ).then((m) => m.SecurityGroupRulesListComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
