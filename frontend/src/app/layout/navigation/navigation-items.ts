/**
 * Main navigation. Each feature spec that adds an area registers its entries here (and its lazy route in
 * app.routes.ts). Entries with `permission` are shown only to users who hold it (UX only).
 */
export interface NavigationItem {
  readonly label: string;
  readonly icon: string;
  readonly route: string;
  readonly permission?: string;
}

export interface NavigationGroup {
  /** Section title; `null` for the ungrouped top section. */
  readonly title: string | null;
  readonly items: readonly NavigationItem[];
}

export const NAVIGATION: readonly NavigationGroup[] = [
  {
    title: null,
    items: [{ label: 'Inicio', icon: 'home', route: '/home' }],
  },
  {
    title: 'Inventario',
    items: [{ label: 'Equipos', icon: 'devices', route: '/inventory', permission: 'devices.read' }],
  },
  {
    title: 'Administración',
    items: [
      { label: 'Usuarios', icon: 'group', route: '/admin/users', permission: 'users.read' },
      { label: 'Políticas', icon: 'policy', route: '/admin/policies', permission: 'policies.read' },
      {
        label: 'Permisos',
        icon: 'key',
        route: '/admin/permissions',
        permission: 'permissions.read',
      },
      { label: 'Auditoría', icon: 'history', route: '/admin/audit', permission: 'audit.read' },
    ],
  },
  {
    title: 'Ayuda',
    items: [{ label: 'Manual de uso', icon: 'menu_book', route: '/manual' }],
  },
];
