import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { AuthService } from '../../auth/services/auth-service';
import { Shell } from './shell';

@Component({ template: '' })
class Blank {}

describe('Shell navigation', () => {
  const permissions = signal<readonly string[]>([]);

  beforeEach(() => {
    permissions.set([]);
    TestBed.configureTestingModule({
      imports: [Shell],
      providers: [
        provideRouter([
          { path: 'home', component: Blank },
          { path: 'manual', component: Blank },
        ]),
        {
          provide: AuthService,
          useValue: {
            session: () => permissions(),
            user: () => ({ displayName: 'Ada', username: null }),
            hasPermission: (permission: string) => permissions().includes(permission),
            logout: () => undefined,
          },
        },
      ],
    });
  });

  const labels = async () => {
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    return [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll('mat-nav-list [matListItemTitle]'),
    ].map((element) => element.textContent?.trim());
  };

  it('hides the administration section from users without permissions', async () => {
    expect(await labels()).toEqual(['Inicio', 'Manual de uso']);
  });

  it('shows only the administration entries the user is allowed to use', async () => {
    permissions.set(['users.read', 'audit.read']);

    expect(await labels()).toEqual(['Inicio', 'Usuarios', 'Auditoría', 'Manual de uso']);
  });

  it('shows the inventory to users who can read devices', async () => {
    permissions.set(['devices.read']);

    expect(await labels()).toEqual(['Inicio', 'Equipos', 'Manual de uso']);
  });

  it('shows the SQL audit instances to users who can read them, after the inventory', async () => {
    permissions.set(['sql-audit.read', 'devices.read']);

    expect(await labels()).toEqual(['Inicio', 'Equipos', 'Instancias', 'Manual de uso']);
  });

  // SPEC-0010 CA-4 and RF-3: the active item carries the orange indicator and `aria-current="page"`; the others carry neither.
  it('marks the active item with aria-current="page" and the indicator', async () => {
    const fixture = TestBed.createComponent(Shell);
    await TestBed.inject(Router).navigateByUrl('/home');
    await fixture.whenStable();
    fixture.detectChanges();

    const items = [
      ...(fixture.nativeElement as HTMLElement).querySelectorAll<HTMLAnchorElement>(
        'mat-nav-list a',
      ),
    ];
    const active = items.filter((item) => item.getAttribute('aria-current') === 'page');
    expect(active.map((item) => item.textContent?.trim())).toEqual(['homeInicio']);
    expect(active[0].classList).toContain('itmas-nav-active');
    expect(
      items
        .filter((item) => item !== active[0])
        .some((item) => item.classList.contains('itmas-nav-active')),
    ).toBe(false);
  });

  it('shows the logo in the navigation island', async () => {
    const fixture = TestBed.createComponent(Shell);
    await fixture.whenStable();
    const island = (fixture.nativeElement as HTMLElement).querySelector('.itmas-glass-dark');
    expect(island?.querySelector('app-logo')?.textContent?.trim()).toBe('hubITMas');
  });
});
