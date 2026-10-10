import { Routes } from '@angular/router';

/** User manual (SPEC-0004). Public: no guard, no permission (P-1). */
export default [
  {
    path: '',
    title: 'Manual de uso',
    loadComponent: () => import('./manual-index-page').then((m) => m.ManualIndexPage),
  },
  {
    path: ':id',
    title: 'Manual de uso',
    loadComponent: () => import('./manual-process-page').then((m) => m.ManualProcessPage),
  },
] satisfies Routes;
