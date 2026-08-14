import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import {
  ApiKeyRevealDialogComponent,
  ApiKeyRevealDialogData
} from './api-key-reveal-dialog.component';

describe('ApiKeyRevealDialogComponent', () => {
  const data: ApiKeyRevealDialogData = {
    hostname: 'PC-001',
    deviceId: 'device-1',
    apiKey: 'device-1.super-secret'
  };
  let snackBarSpy: jasmine.SpyObj<MatSnackBar>;

  beforeEach(async () => {
    snackBarSpy = jasmine.createSpyObj('MatSnackBar', ['open']);

    await TestBed.configureTestingModule({
      imports: [ApiKeyRevealDialogComponent],
      providers: [
        provideAnimationsAsync(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: jasmine.createSpyObj('MatDialogRef', ['close']) },
        { provide: MatSnackBar, useValue: snackBarSpy }
      ]
    }).compileComponents();
  });

  function createComponent() {
    const fixture = TestBed.createComponent(ApiKeyRevealDialogComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('exposes the injected dialog data (the one-time apiKey) to its template', () => {
    const fixture = createComponent();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain(data.hostname);
    expect(text).toContain(data.apiKey);
  });

  it('shows a success snackbar when the copy succeeds', () => {
    const fixture = createComponent();
    fixture.componentInstance['onCopied'](true);
    expect(snackBarSpy.open).toHaveBeenCalledWith(
      'Clave copiada al portapapeles.',
      'Cerrar',
      { duration: 3000 }
    );
  });

  it('shows a failure snackbar when the copy fails', () => {
    const fixture = createComponent();
    fixture.componentInstance['onCopied'](false);
    expect(snackBarSpy.open).toHaveBeenCalledWith(
      'No se pudo copiar la clave.',
      'Cerrar',
      { duration: 3000 }
    );
  });
});
