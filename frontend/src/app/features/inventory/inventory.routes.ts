import { MatPaginatorIntl } from '@angular/material/paginator';
import { Routes } from '@angular/router';
import { permissionGuard } from '../../auth/guards/permission-guard';
import { SpanishPaginatorIntl } from '../../core/i18n/spanish-paginator-intl';

/** Collected inventory needs both permissions: the device header and its reports (SPEC-0003 §7). UX only. */
const REPORTS = ['devices.read', 'devices.reports.read'] as const;

/** Device inventory (SPEC-0003). */
export default [
  {
    path: '',
    // Route-level provider: keeps the paginator out of the initial bundle.
    providers: [{ provide: MatPaginatorIntl, useClass: SpanishPaginatorIntl }],
    children: [
      {
        path: '',
        title: 'Equipos',
        canActivate: [permissionGuard],
        data: { permission: 'devices.read' },
        loadComponent: () => import('./devices/devices-page').then((m) => m.DevicesPage),
      },
      {
        path: ':id/reports',
        title: 'Registros del equipo',
        canActivate: [permissionGuard],
        data: { permission: REPORTS },
        loadComponent: () =>
          import('./history/device-history-page').then((m) => m.DeviceHistoryPage),
      },
      {
        path: ':id/reports/latest',
        title: 'Informe del equipo',
        canActivate: [permissionGuard],
        data: { permission: REPORTS },
        loadComponent: () => import('./report/device-report-page').then((m) => m.DeviceReportPage),
      },
      {
        path: ':id/reports/:reportId',
        title: 'Informe del equipo',
        canActivate: [permissionGuard],
        data: { permission: REPORTS },
        loadComponent: () => import('./report/device-report-page').then((m) => m.DeviceReportPage),
      },
    ],
  },
] satisfies Routes;
