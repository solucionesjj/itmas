import { TestBed } from '@angular/core/testing';
import { MatDialogRef } from '@angular/material/dialog';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { DeviceFormDialogComponent } from './device-form-dialog.component';

describe('DeviceFormDialogComponent', () => {
  let dialogRefSpy: jasmine.SpyObj<MatDialogRef<DeviceFormDialogComponent>>;

  beforeEach(async () => {
    dialogRefSpy = jasmine.createSpyObj('MatDialogRef', ['close']);

    await TestBed.configureTestingModule({
      imports: [DeviceFormDialogComponent],
      providers: [
        provideAnimationsAsync(),
        { provide: MatDialogRef, useValue: dialogRefSpy }
      ]
    }).compileComponents();
  });

  function createComponent() {
    const fixture = TestBed.createComponent(DeviceFormDialogComponent);
    fixture.detectChanges();
    return fixture.componentInstance;
  }

  it('defaults category to collaborator and requires a hostname', () => {
    const component = createComponent();
    expect(component.form.value.category).toBe('collaborator');
    expect(component.form.invalid).toBe(true);
  });

  it('does not close the dialog when submitted with an empty hostname', () => {
    const component = createComponent();
    component.submit();
    expect(dialogRefSpy.close).not.toHaveBeenCalled();
  });

  it('closes the dialog with the form value on a valid submit', () => {
    const component = createComponent();
    component.form.setValue({ hostname: 'PC-001', category: 'infrastructure' });
    component.submit();
    expect(dialogRefSpy.close).toHaveBeenCalledWith({
      hostname: 'PC-001',
      category: 'infrastructure'
    });
  });

  it('closes the dialog with no result on cancel', () => {
    const component = createComponent();
    component.cancel();
    expect(dialogRefSpy.close).toHaveBeenCalledWith();
  });
});
