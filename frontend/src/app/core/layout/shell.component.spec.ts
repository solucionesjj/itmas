import { BreakpointObserver, BreakpointState } from '@angular/cdk/layout';
import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { JwtPayload, UserRole } from '../models/auth.models';
import { AuthService } from '../services/auth.service';
import { ShellComponent } from './shell.component';

/** Drives the shell's window-size class from a test. */
class FakeBreakpointObserver {
  readonly state$ = new BehaviorSubject<BreakpointState>({ matches: false, breakpoints: {} });

  observe(): BehaviorSubject<BreakpointState> {
    return this.state$;
  }

  emit(breakpoints: Record<string, boolean>): void {
    this.state$.next({ matches: Object.values(breakpoints).some(Boolean), breakpoints });
  }
}

/** Stands in for a feature page; the shell only cares that a route resolved. */
@Component({ selector: 'app-stub-page', standalone: true, template: '' })
class StubPageComponent {}

const COMPACT = '(max-width: 599.98px)';
const MEDIUM = '(min-width: 600px) and (max-width: 904.98px)';

function payload(role: UserRole): JwtPayload {
  return {
    sub: 'id',
    username: 'tester',
    role,
    mustChangePassword: false,
    iat: 0,
    exp: Number.MAX_SAFE_INTEGER
  };
}

describe('ShellComponent', () => {
  let fixture: ComponentFixture<ShellComponent>;
  let breakpoints: FakeBreakpointObserver;
  let currentUser: ReturnType<typeof signal<JwtPayload | null>>;

  function setup(role: UserRole = 'administrator') {
    currentUser = signal<JwtPayload | null>(payload(role));
    breakpoints = new FakeBreakpointObserver();

    TestBed.configureTestingModule({
      imports: [ShellComponent],
      providers: [
        provideRouter([]),
        { provide: BreakpointObserver, useValue: breakpoints },
        { provide: AuthService, useValue: { currentUser, logout: () => of(undefined) } }
      ]
    });

    fixture = TestBed.createComponent(ShellComponent);
    fixture.detectChanges();
  }

  function navLabels(): string[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.nav-item__label')).map((el) =>
      (el as HTMLElement).textContent!.trim()
    );
  }

  function drawer(): HTMLElement {
    return fixture.nativeElement.querySelector('mat-sidenav');
  }

  function menuButton(): HTMLButtonElement | null {
    return fixture.nativeElement.querySelector('mat-toolbar button[aria-label*="navegación"]');
  }

  describe('navigation by role', () => {
    it('shows every section to an Administrador', () => {
      setup('administrator');
      expect(navLabels()).toEqual([
        'Panel',
        'Equipos',
        'Alertas',
        'Reportes',
        'Reglas de Firewall AWS',
        'Usuarios'
      ]);
    });

    it('hides Alertas and Usuarios from a Usuario', () => {
      setup('user');
      const labels = navLabels();
      expect(labels).not.toContain('Alertas');
      expect(labels).not.toContain('Usuarios');
      expect(labels).toContain('Equipos');
    });

    it('shows Alertas but not Usuarios to an Auditor', () => {
      setup('auditor');
      expect(navLabels()).toContain('Alertas');
      expect(navLabels()).not.toContain('Usuarios');
    });
  });

  describe('sidenav across window size classes', () => {
    it('is a persistent 264px drawer above the Medium class', () => {
      setup();
      breakpoints.emit({ [COMPACT]: false, [MEDIUM]: false });
      fixture.detectChanges();

      expect(drawer().classList).toContain('mat-drawer-side');
      expect(drawer().classList).toContain('mat-drawer-opened');
      expect(drawer().querySelector('.shell__nav--rail')).toBeNull();
      expect(menuButton()).toBeNull();
    });

    it('becomes an icon-only rail in the Medium class', () => {
      setup();
      breakpoints.emit({ [COMPACT]: false, [MEDIUM]: true });
      fixture.detectChanges();

      expect(fixture.nativeElement.querySelector('.shell__nav--rail')).toBeTruthy();
      expect(drawer().classList).toContain('mat-drawer-side');
      // The label stays in the DOM even when clipped, so the link keeps its name.
      expect(navLabels()).toContain('Panel');
    });

    it('floats over the content and starts closed in the Compact class', () => {
      setup();
      breakpoints.emit({ [COMPACT]: true, [MEDIUM]: false });
      fixture.detectChanges();

      expect(drawer().classList).toContain('mat-drawer-over');
      expect(drawer().classList).not.toContain('mat-drawer-opened');
      expect(menuButton()).toBeTruthy();
    });
  });

  describe('drawer toggling', () => {
    // Regression: the menu button used to call MatDrawer's imperative `toggle()`
    // while `[opened]` was bound to a signal. `toggle()` left the signal untouched,
    // so the two drifted, and a later write of the value the signal already held
    // was a no-op — tapping a nav item then failed to close the drawer.
    it('closes after tapping a nav item in the Compact class', () => {
      setup();
      breakpoints.emit({ [COMPACT]: true, [MEDIUM]: false });
      fixture.detectChanges();

      menuButton()!.click();
      fixture.detectChanges();
      expect(drawer().classList).toContain('mat-drawer-opened');

      const link: HTMLElement = fixture.nativeElement.querySelector('.nav-item');
      link.click();
      fixture.detectChanges();

      expect(drawer().classList).not.toContain('mat-drawer-opened');
    });

    it('toggles both ways from the menu button', () => {
      setup();
      breakpoints.emit({ [COMPACT]: true, [MEDIUM]: false });
      fixture.detectChanges();

      menuButton()!.click();
      fixture.detectChanges();
      expect(drawer().classList).toContain('mat-drawer-opened');

      menuButton()!.click();
      fixture.detectChanges();
      expect(drawer().classList).not.toContain('mat-drawer-opened');
    });

    it('keeps the drawer open when a nav item is tapped above the Compact class', () => {
      setup();
      breakpoints.emit({ [COMPACT]: false, [MEDIUM]: false });
      fixture.detectChanges();

      const link: HTMLElement = fixture.nativeElement.querySelector('.nav-item');
      link.click();
      fixture.detectChanges();

      expect(drawer().classList).toContain('mat-drawer-opened');
    });
  });
  describe('active section', () => {
    function setupWithRoutes() {
      currentUser = signal<JwtPayload | null>(payload('administrator'));
      breakpoints = new FakeBreakpointObserver();

      TestBed.configureTestingModule({
        imports: [ShellComponent],
        providers: [
          provideRouter([
            { path: '', component: StubPageComponent, title: 'nav.dashboard' },
            { path: 'devices', component: StubPageComponent, title: 'nav.devices' }
          ]),
          { provide: BreakpointObserver, useValue: breakpoints },
          { provide: AuthService, useValue: { currentUser, logout: () => of(undefined) } }
        ]
      });

      fixture = TestBed.createComponent(ShellComponent);
      fixture.detectChanges();
    }

    function activeLabels(): string[] {
      return Array.from(fixture.nativeElement.querySelectorAll('.nav-item--active .nav-item__label')).map((el) =>
        (el as HTMLElement).textContent!.trim()
      );
    }

    // Both halves of this — the highlight and the toolbar title — hang off the
    // router's NavigationEnd. A page that navigates while it is being activated
    // turns that event into NavigationCancel + NavigationSkipped, which is how the
    // sidebar came to highlight the previous section while the next one was on
    // screen. See syncFiltersToUrl in core/utils/filter-url.util.ts.
    it('highlights the routed section and names it in the toolbar', async () => {
      setupWithRoutes();
      const router = TestBed.inject(Router);

      await router.navigate(['/devices']);
      await fixture.whenStable();
      fixture.detectChanges();

      expect(activeLabels()).toEqual(['Equipos']);
      expect(
        (fixture.nativeElement.querySelector('.shell__title') as HTMLElement).textContent!.trim()
      ).toBe('Equipos');
    });
  });
});
