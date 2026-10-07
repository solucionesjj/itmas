import { Component, inject } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { Permission, PermissionMetadata } from '../data/authorization.models';

/** Edits the presentation metadata of a permission. The permission name is immutable (SPEC-0001 RF-9). */
@Component({
  selector: 'app-edit-permission-dialog',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    ReactiveFormsModule,
  ],
  template: `
    <h2 mat-dialog-title>Editar permiso</h2>
    <form [formGroup]="form" (ngSubmit)="save()">
      <mat-dialog-content>
        <p>
          <code>{{ permission.name }}</code>
        </p>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Nombre visible</mat-label>
          <input matInput formControlName="displayName" maxlength="100" required />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Descripción</mat-label>
          <textarea matInput formControlName="description" maxlength="500" rows="3"></textarea>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Categoría</mat-label>
          <input matInput formControlName="category" maxlength="100" required />
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancelar</button>
        <button mat-flat-button type="submit" [disabled]="form.invalid">Guardar</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .full-width {
      width: 100%;
      min-width: min(420px, 70vw);
    }
  `,
})
export class EditPermissionDialog {
  private readonly dialogRef =
    inject<MatDialogRef<EditPermissionDialog, PermissionMetadata>>(MatDialogRef);
  protected readonly permission = inject<Permission>(MAT_DIALOG_DATA);

  protected readonly form = new FormGroup({
    displayName: new FormControl(this.permission.displayName, {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    description: new FormControl(this.permission.description, {
      nonNullable: true,
      validators: [Validators.maxLength(500)],
    }),
    category: new FormControl(this.permission.category, {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
  });

  protected save(): void {
    if (this.form.valid) {
      const value = this.form.getRawValue();
      this.dialogRef.close({
        displayName: value.displayName.trim(),
        description: value.description.trim(),
        category: value.category.trim(),
      });
    }
  }
}
