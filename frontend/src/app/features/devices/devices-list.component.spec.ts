import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
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
