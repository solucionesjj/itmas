import { MatPaginatorIntl } from '@angular/material/paginator';
import { Routes } from '@angular/router';
import { permissionGuard } from '../../auth/guards/permission-guard';
import { SpanishPaginatorIntl } from '../../core/i18n/spanish-paginator-intl';

/** Every screen of the section needs `sql-audit.read` (SPEC-0005 §7). UX only: the API enforces it. */
const READ = { permission: 'sql-audit.read' };
const TITLE = 'Auditoría SQL';

/** SQL engine access audit (SPEC-0005 §11). */
export default [
  {
    path: '',
    // Route-level provider: keeps the paginator out of the initial bundle.
    providers: [{ provide: MatPaginatorIntl, useClass: SpanishPaginatorIntl }],
    children: [
      {
        path: '',
        title: TITLE,
        canActivate: [permissionGuard],
        data: READ,
        loadComponent: () => import('./instances/instances-page').then((m) => m.InstancesPage),
      },
      {
        path: 'instances/:id',
        title: TITLE,
        canActivate: [permissionGuard],
        data: READ,
        loadComponent: () => import('./instance/instance-page').then((m) => m.InstancePage),
      },
      {
        path: 'accounts/:id',
        title: TITLE,
        canActivate: [permissionGuard],
        data: READ,
        loadComponent: () => import('./account/account-page').then((m) => m.AccountPage),
      },
    ],
  },
] satisfies Routes;
