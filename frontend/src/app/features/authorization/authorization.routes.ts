import { MatPaginatorIntl } from '@angular/material/paginator';
import { Routes } from '@angular/router';
import { permissionGuard } from '../../auth/guards/permission-guard';
import { SpanishPaginatorIntl } from '../../core/i18n/spanish-paginator-intl';

/** Authorization administration (SPEC-0001). Every route requires the read permission of its area (UX only). */
export default [
  {
    path: '',
    // Route-level provider: keeps the paginator out of the initial bundle.
    providers: [{ provide: MatPaginatorIntl, useClass: SpanishPaginatorIntl }],
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'users' },
      {
        path: 'users',
        title: 'Usuarios',
        canActivate: [permissionGuard],
        data: { permission: 'users.read' },
        loadComponent: () => import('./users/users-page').then((m) => m.UsersPage),
      },
      {
        path: 'users/:id',
        title: 'Usuario',
        canActivate: [permissionGuard],
        data: { permission: 'users.read' },
        loadComponent: () => import('./users/user-detail-page').then((m) => m.UserDetailPage),
      },
      {
        path: 'policies',
        title: 'Políticas',
        canActivate: [permissionGuard],
        data: { permission: 'policies.read' },
        loadComponent: () => import('./policies/policies-page').then((m) => m.PoliciesPage),
      },
      {
        path: 'policies/new',
        title: 'Nueva política',
        canActivate: [permissionGuard],
        data: { permission: 'policies.create' },
        loadComponent: () =>
          import('./policies/policy-editor-page').then((m) => m.PolicyEditorPage),
      },
      {
        path: 'policies/:id',
        title: 'Política',
        canActivate: [permissionGuard],
        data: { permission: 'policies.read' },
        loadComponent: () =>
          import('./policies/policy-detail-page').then((m) => m.PolicyDetailPage),
      },
      {
        path: 'policies/:id/edit',
        title: 'Editar política',
        canActivate: [permissionGuard],
        data: { permission: 'policies.update' },
        loadComponent: () =>
          import('./policies/policy-editor-page').then((m) => m.PolicyEditorPage),
      },
      {
        path: 'permissions',
        title: 'Permisos',
        canActivate: [permissionGuard],
        data: { permission: 'permissions.read' },
        loadComponent: () =>
          import('./permissions/permissions-page').then((m) => m.PermissionsPage),
      },
      {
        path: 'audit',
        title: 'Auditoría',
        canActivate: [permissionGuard],
        data: { permission: 'audit.read' },
        loadComponent: () => import('./audit/audit-page').then((m) => m.AuditPage),
      },
    ],
  },
] satisfies Routes;
