import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import {
  RotateKeyConfirmDialogComponent,
  RotateKeyConfirmDialogData
} from './rotate-key-confirm-dialog.component';

describe('RotateKeyConfirmDialogComponent', () => {
  const data: RotateKeyConfirmDialogData = { hostname: 'PC-001' };
  let dialogRefSpy: jasmine.SpyObj<MatDialogRef<RotateKeyConfirmDialogComponent>>;

  beforeEach(async () => {
    dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [RotateKeyConfirmDialogComponent],
      providers: [
        provideAnimationsAsync(),
        { provide: MAT_DIALOG_DATA, useValue: data },
        { provide: MatDialogRef, useValue: dialogRefSpy }
      ]
    }).compileComponents();
  });

  function createComponent() {
    const fixture = TestBed.createComponent(RotateKeyConfirmDialogComponent);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('renders the target hostname', () => {
    const fixture = TestBed.createComponent(RotateKeyConfirmDialogComponent);
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('PC-001');
  });

  it('closes with true on confirm', () => {
    const component = createComponent();
    component.confirm();
    expect(dialogRefSpy.close).toHaveBeenCalledWith(true);
  });

  it('closes with false on cancel', () => {
    const component = createComponent();
    component.cancel();
    expect(dialogRefSpy.close).toHaveBeenCalledWith(false);
  });
});
