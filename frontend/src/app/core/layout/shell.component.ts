import { BreakpointObserver } from '@angular/cdk/layout';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import {
  ActivatedRouteSnapshot,
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet
} from '@angular/router';
import { filter, startWith } from 'rxjs';
import { UserRole } from '../models/auth.models';
import { AuthService } from '../services/auth.service';
import { ThemeService } from '../services/theme.service';

/** design.md §6.3 window size classes. Compact < 600, Medium 600–904. */
const COMPACT = '(max-width: 599.98px)';
const MEDIUM = '(min-width: 600px) and (max-width: 904.98px)';

interface NavItem {
  readonly route: string;
  readonly label: string;
  /** Canonical icon per concept from design.md §13.1 — do not substitute synonyms. */
  readonly icon: string;
  /** Absent = every authenticated role. Hiding a link is UX only; the route's own
   *  guard is the real enforcement (role.guard.ts). */
  readonly roles?: readonly UserRole[];
  /** Only the dashboard needs exact matching, or '/' would match every route. */
  readonly exact?: boolean;
}

const NAV_ITEMS: readonly NavItem[] = [
  { route: '/', label: 'Panel', icon: 'space_dashboard', exact: true },
  { route: '/devices', label: 'Equipos', icon: 'devices' },
  { route: '/alerts', label: 'Alertas', icon: 'notifications', roles: ['administrator', 'auditor'] },
  { route: '/reports', label: 'Reportes', icon: 'assessment' },
  { route: '/security-group-rules', label: 'Reglas de Firewall AWS', icon: 'shield' },
  { route: '/admin/users', label: 'Usuarios', icon: 'manage_accounts', roles: ['administrator'] }
];

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatSidenavModule
  ],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  protected readonly authService = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  private readonly router = inject(Router);
  private readonly breakpoints = inject(BreakpointObserver);

  /** Compact: sidenav floats over the content and starts closed. */
  protected readonly isCompact = signal(false);
  /** Medium: 72px icon-only rail. */
  protected readonly isRail = signal(false);
  protected readonly sidenavOpen = signal(true);

  protected readonly sidenavMode = computed<'over' | 'side'>(() =>
    this.isCompact() ? 'over' : 'side'
  );

  protected readonly pageTitle = signal('');
  /** §7: the toolbar is flat at rest and gains elevation 2 once <main> scrolls. */
  protected readonly scrolled = signal(false);

  protected readonly navItems = computed(() => {
    const role = this.authService.currentUser()?.role;
    return NAV_ITEMS.filter((item) => !item.roles || (role && item.roles.includes(role)));
  });

  constructor() {
    // One subscription for both classes, so `isCompact`/`isRail`/`sidenavOpen`
    // can never disagree about which breakpoint is active.
    this.breakpoints
      .observe([COMPACT, MEDIUM])
      .pipe(takeUntilDestroyed())
      .subscribe((state) => {
        const compact = state.breakpoints[COMPACT] ?? false;
        this.isCompact.set(compact);
        this.isRail.set(state.breakpoints[MEDIUM] ?? false);
        this.sidenavOpen.set(!compact);
      });

    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        startWith(null),
        takeUntilDestroyed()
      )
      .subscribe(() => {
        // Same resolved value ItmasTitleStrategy puts in the document title, so the
        // toolbar and the tab can never disagree.
        this.pageTitle.set(this.resolveTitle(this.router.routerState.snapshot.root));
      });
  }

  /** Deepest route title wins, so a child overrides its parent's. */
  private resolveTitle(route: ActivatedRouteSnapshot): string {
    let current: ActivatedRouteSnapshot | null = route;
    let title = '';
    while (current) {
      title = current.title ?? title;
      current = current.firstChild;
    }
    return title;
  }

  /**
   * The signal is the single source of truth for the drawer. design.md §9.10's
   * snippet calls `nav.toggle()` on the menu button while also binding
   * `[opened]="sidenavOpen()"`, which is a latent bug: `toggle()` mutates
   * MatDrawer's internal state without touching the signal, so the two drift.
   * Once they have drifted, setting the signal to a value it already holds is a
   * no-op (signals don't emit on an equal write) and the drawer never closes.
   */
  protected toggleSidenav(): void {
    this.sidenavOpen.update((open) => !open);
  }

  protected onContentScroll(event: Event): void {
    this.scrolled.set((event.target as HTMLElement).scrollTop > 0);
  }

  protected closeIfOver(): void {
    // Tapping a nav item on a phone should not leave the drawer covering the page.
    if (this.isCompact()) {
      this.sidenavOpen.set(false);
    }
  }

  logout(): void {
    this.authService.logout().subscribe(() => this.router.navigate(['/login']));
  }
}
