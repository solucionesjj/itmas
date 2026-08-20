import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { Router, provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { DevicesService } from './devices.service';
import { DevicesListComponent } from './devices-list.component';

describe('DevicesListComponent', () => {
  let devicesServiceSpy: jasmine.SpyObj<DevicesService>;
  let authServiceSpy: jasmine.SpyObj<AuthService>;
  let dialogSpy: jasmine.SpyObj<MatDialog>;
  let snackBarSpy: jasmine.SpyObj<MatSnackBar>;

  function configure(role: 'administrator' | 'user' | 'auditor') {
    devicesServiceSpy = jasmine.createSpyObj('DevicesService', [
      'list',
      'create',
      'rotateKey'
    ]);
    devicesServiceSpy.list.and.returnValue(
      of({ items: [], total: 0, page: 1, limit: 20 })
    );
    authServiceSpy = jasmine.createSpyObj('AuthService', ['currentUser']);
    authServiceSpy.currentUser.and.returnValue({
      sub: 'user-1',
      username: 'tester',
      role,
      mustChangePassword: false,
      iat: 0,
      exp: 0
    });
    dialogSpy = jasmine.createSpyObj('MatDialog', ['open']);
    snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);

    TestBed.configureTestingModule({
      imports: [DevicesListComponent],
      providers: [
        provideAnimationsAsync(),
        // The view reflects its filters into the URL (§10.1), so it injects
        // Router/ActivatedRoute.
        provideRouter([]),
        { provide: DevicesService, useValue: devicesServiceSpy },
        { provide: AuthService, useValue: authServiceSpy },
        { provide: MatDialog, useValue: dialogSpy },
        { provide: MatSnackBar, useValue: snackBarSpy }
      ]
    });
  }

  // Regression: the view used to reflect its filters into the URL from `reload()`,
  // and the first `reload()` runs in the constructor — i.e. during the very
  // navigation that is activating this route. That `router.navigate()` superseded
  // the in-flight navigation, so it ended as NavigationCancel and the router never
  // emitted NavigationEnd; the sidebar (`routerLinkActive`) and the shell's toolbar
  // title both listen for NavigationEnd only, so they kept showing the *previous*
  // page while this one was on screen. See syncFiltersToUrl in filter-url.util.ts.
  it('does not touch the URL while it is being routed to', () => {
    configure('administrator');
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    TestBed.createComponent(DevicesListComponent);

    expect(devicesServiceSpy.list).toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('writes the filters to the URL once a filter changes', fakeAsync(() => {
    configure('administrator');
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(DevicesListComponent);

    fixture.componentInstance['filters'].patchValue({ hostname: 'PC-001' });
    tick(300);

    expect(navigate).toHaveBeenCalledTimes(1);
    const [commands, extras] = navigate.calls.mostRecent().args as [
      unknown[],
      { queryParams: Record<string, string | undefined>; replaceUrl: boolean }
    ];
    expect(commands).toEqual([]);
    expect(extras.queryParams['hostname']).toBe('PC-001');
    // A cleared filter leaves the URL rather than sitting in it as `?osName=`.
    expect(extras.queryParams['osName']).toBeUndefined();
    expect(extras.replaceUrl).toBe(true);
  }));

  it('shows the actions column for an Administrador', () => {
    configure('administrator');
    const fixture = TestBed.createComponent(DevicesListComponent);
    const component = fixture.componentInstance;
    expect(component['isAdmin']).toBe(true);
    expect(component['displayedColumns']).toContain('actions');
  });

  it('hides the actions column for a Usuario', () => {
    configure('user');
    const fixture = TestBed.createComponent(DevicesListComponent);
    const component = fixture.componentInstance;
    expect(component['isAdmin']).toBe(false);
    expect(component['displayedColumns']).not.toContain('actions');
  });

  it('hides the actions column for an Auditor', () => {
    configure('auditor');
    const fixture = TestBed.createComponent(DevicesListComponent);
    const component = fixture.componentInstance;
    expect(component['isAdmin']).toBe(false);
    expect(component['displayedColumns']).not.toContain('actions');
  });

  it('opens the one-time key reveal dialog after a successful device creation', () => {
    configure('administrator');

    const created = {
      deviceId: 'device-1',
      hostname: 'PC-001',
      category: 'collaborator' as const,
      apiKey: 'device-1.secret'
    };
    devicesServiceSpy.create.and.returnValue(of(created));

    const createDialogRef = {
      afterClosed: () => of({ hostname: 'PC-001', category: 'collaborator' })
    };
    const revealDialogRef = { afterClosed: () => of(undefined) };
    dialogSpy.open.and.returnValues(createDialogRef as never, revealDialogRef as never);

    const fixture = TestBed.createComponent(DevicesListComponent);
    const component = fixture.componentInstance;
    component['openCreateDialog']();

    expect(devicesServiceSpy.create).toHaveBeenCalledWith({
      hostname: 'PC-001',
      category: 'collaborator'
    });
    expect(dialogSpy.open).toHaveBeenCalledTimes(2);
  });

  it('shows an error snackbar when device creation fails', () => {
    configure('administrator');

    devicesServiceSpy.create.and.returnValue(
      throwError(() => ({ error: { error: { message: 'boom' } } }))
    );
    const createDialogRef = {
      afterClosed: () => of({ hostname: 'PC-001', category: 'collaborator' })
    };
    dialogSpy.open.and.returnValue(createDialogRef as never);

    const fixture = TestBed.createComponent(DevicesListComponent);
    const component = fixture.componentInstance;
    component['openCreateDialog']();

    expect(snackBarSpy.open).toHaveBeenCalledWith('boom', 'Cerrar', { duration: 4000 });
  });

  it('rotates the key after confirmation and reveals the new one-time apiKey', () => {
    configure('administrator');

    const rotated = { deviceId: 'device-1', apiKey: 'device-1.new-secret' };
    devicesServiceSpy.rotateKey.and.returnValue(of(rotated));

    const confirmDialogRef = { afterClosed: () => of(true) };
    const revealDialogRef = { afterClosed: () => of(undefined) };
    dialogSpy.open.and.returnValues(confirmDialogRef as never, revealDialogRef as never);

    const fixture = TestBed.createComponent(DevicesListComponent);
    const component = fixture.componentInstance;
    component['openRotateKeyDialog']({
      _id: 'device-1',
      hostname: 'PC-001',
      category: 'collaborator'
    });

    expect(devicesServiceSpy.rotateKey).toHaveBeenCalledWith('device-1');
    expect(dialogSpy.open).toHaveBeenCalledTimes(2);
    expect(snackBarSpy.open).toHaveBeenCalledWith('Clave rotada correctamente.', 'Cerrar', {
      duration: 3000
    });
  });

  it('does not rotate the key when the confirmation dialog is dismissed', () => {
    configure('administrator');

    const confirmDialogRef = { afterClosed: () => of(false) };
    dialogSpy.open.and.returnValue(confirmDialogRef as never);

    const fixture = TestBed.createComponent(DevicesListComponent);
    const component = fixture.componentInstance;
    component['openRotateKeyDialog']({
      _id: 'device-1',
      hostname: 'PC-001',
      category: 'collaborator'
    });

    expect(devicesServiceSpy.rotateKey).not.toHaveBeenCalled();
  });
});
