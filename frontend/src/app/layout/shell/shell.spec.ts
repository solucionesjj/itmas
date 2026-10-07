import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from '../../auth/services/auth-service';
import { Shell } from './shell';

describe('Shell navigation', () => {
  const permissions = signal<readonly string[]>([]);

  beforeEach(() => {
    permissions.set([]);
    TestBed.configureTestingModule({
      imports: [Shell],
      providers: [
        provideRouter([]),
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
    expect(await labels()).toEqual(['Inicio']);
  });

  it('shows only the administration entries the user is allowed to use', async () => {
    permissions.set(['users.read', 'audit.read']);

    expect(await labels()).toEqual(['Inicio', 'Usuarios', 'Auditoría']);
  });
});
