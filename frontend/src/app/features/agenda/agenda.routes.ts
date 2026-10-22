import { Routes } from '@angular/router';
import { permissionGuard } from '../../auth/guards/permission-guard';
import { provideAgendaDates } from './data/agenda-date-adapter';

/**
 * Every screen needs `agenda.read`, and the ones that change something its own permission too (SPEC-0006 §7, §11). UX only:
 * the API enforces them.
 */
const READ = 'agenda.read';
const guard = (...permissions: string[]) => ({
  canActivate: [permissionGuard],
  data: { permission: [READ, ...permissions] },
});

/** Infrastructure agenda (SPEC-0006). */
export default [
  {
    path: '',
    // Route-level provider: keeps the date adapter out of the initial bundle and the other areas untouched.
    providers: [provideAgendaDates()],
    children: [
      {
        path: '',
        title: 'Agenda de infraestructura',
        ...guard(),
        loadComponent: () => import('./page/agenda-page').then((m) => m.AgendaPage),
      },
      {
        path: 'events/new',
        title: 'Nueva actividad',
        ...guard('agenda.create'),
        loadComponent: () => import('./events/event-page').then((m) => m.EventPage),
      },
      {
        path: 'series/new',
        title: 'Nueva serie',
        ...guard('agenda.create'),
        loadComponent: () => import('./series/series-page').then((m) => m.SeriesPage),
      },
      {
        path: 'events/:id',
        title: 'Actividad',
        ...guard(),
        loadComponent: () => import('./detail/event-detail-page').then((m) => m.EventDetailPage),
      },
      {
        path: 'events/:id/edit',
        title: 'Editar actividad',
        ...guard('agenda.update'),
        loadComponent: () => import('./events/event-page').then((m) => m.EventPage),
      },
      {
        path: 'types',
        title: 'Tipos de actividad',
        ...guard('agenda.types.manage'),
        loadComponent: () => import('./types/types-page').then((m) => m.TypesPage),
      },
    ],
  },
] satisfies Routes;
