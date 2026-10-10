import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { AuthService } from '../services/auth-service';
import { permissionGuard } from './permission-guard';

describe('permissionGuard', () => {
  const granted = new Set<string>();

  beforeEach(() => {
    granted.clear();
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: { hasPermission: (permission: string) => granted.has(permission) },
        },
      ],
    });
  });

  const run = (permission?: string | readonly string[]) =>
    TestBed.runInInjectionContext(() =>
      permissionGuard(
        { data: permission ? { permission } : {} } as ActivatedRouteSnapshot,
        {} as RouterStateSnapshot,
      ),
    );

  it('allows routes without a permission requirement', () => {
    expect(run()).toBe(true);
  });

  it('allows users holding the permission', () => {
    granted.add('users.read');

    expect(run('users.read')).toBe(true);
  });

  it('sends users without the permission to the access denied page', () => {
    expect(TestBed.inject(Router).serializeUrl(run('users.read') as UrlTree)).toBe('/forbidden');
  });

  it('requires every permission of a list', () => {
    granted.add('devices.read');

    expect(run(['devices.read', 'devices.reports.read'])).not.toBe(true);
    granted.add('devices.reports.read');
    expect(run(['devices.read', 'devices.reports.read'])).toBe(true);
  });
});
