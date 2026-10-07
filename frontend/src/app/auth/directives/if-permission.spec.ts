import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AuthService } from '../services/auth-service';
import { IfPermission } from './if-permission';

@Component({
  imports: [IfPermission],
  template: `<button *appIfPermission="'users.update'">Desactivar</button>`,
})
class Host {}

describe('IfPermission', () => {
  const permissions = signal<readonly string[]>([]);

  beforeEach(() => {
    permissions.set([]);
    TestBed.configureTestingModule({
      imports: [Host],
      providers: [
        {
          provide: AuthService,
          useValue: { hasPermission: (permission: string) => permissions().includes(permission) },
        },
      ],
    });
  });

  it('renders the content only while the user holds the permission', async () => {
    const fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('button')).toBeNull();

    permissions.set(['users.update']);
    await fixture.whenStable();
    expect(fixture.nativeElement.querySelector('button')).not.toBeNull();
  });
});
