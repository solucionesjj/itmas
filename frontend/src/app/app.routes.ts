import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';
import { administradorGuard, adminOrAuditorGuard } from './core/guards/role.guard';

// Page titles live here and nowhere else (design.md §7), as message *keys* since
// step 7 — ItmasTitleStrategy translates them, so the tab title follows the
// language like everything else. The router's native
// `title` drives the document title through ItmasTitleStrategy (and so through
// Angular's Title service), and the shell reads the same value for its toolbar;
// each page's own <h1> matches it (§3.3). §7's snippet writes these under
// `data.title`, but the shell is not instantiated for login/change-password, so a
// shell-read `data.title` left those two routes without a document title —
// `title` is the same single source and works on every route. Literal strings
// for now: they become i18n keys in step 7 of design.md §14.
export const routes: Routes = [
  {
    path: 'login',
    title: 'login.title',
    loadComponent: () =>
      import('./features/login/login.component').then((m) => m.LoginComponent)
  },
  {
    path: 'change-password',
    canActivate: [authGuard],
    title: 'changePassword.title',
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
        title: 'nav.dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then(
            (m) => m.DashboardComponent
          )
      },
      {
        path: 'devices',
        title: 'nav.devices',
        loadComponent: () =>
          import('./features/devices/devices-list.component').then(
            (m) => m.DevicesListComponent
          )
      },
      {
        path: 'alerts',
        canActivate: [adminOrAuditorGuard],
        title: 'nav.alerts',
        loadComponent: () =>
          import('./features/alerts/alerts-list.component').then(
            (m) => m.AlertsListComponent
          )
      },
      {
        path: 'admin/users',
        canActivate: [administradorGuard],
        title: 'nav.users',
        loadComponent: () =>
          import('./features/admin/users/users-list.component').then(
            (m) => m.UsersListComponent
          )
      },
      {
        path: 'reports',
        title: 'nav.reports',
        loadComponent: () =>
          import('./features/reports/reports.component').then(
            (m) => m.ReportsComponent
          )
      },
      {
        path: 'security-group-rules',
        title: 'nav.firewallRules',
        loadComponent: () =>
          import(
            './features/security-group-rules/security-group-rules-list.component'
          ).then((m) => m.SecurityGroupRulesListComponent)
      }
    ]
  },
  { path: '**', redirectTo: '' }
];
