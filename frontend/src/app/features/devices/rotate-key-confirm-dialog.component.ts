import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { TranslatePipe } from '../../core/i18n/t.pipe';

export interface RotateKeyConfirmDialogData {
  hostname: string;
}

@Component({
  selector: 'app-rotate-key-confirm-dialog',
  standalone: true,
  imports: [TranslatePipe, MatDialogModule, MatButtonModule],
  templateUrl: './rotate-key-confirm-dialog.component.html',
  styleUrl: './rotate-key-confirm-dialog.component.scss'
})
export class RotateKeyConfirmDialogComponent {
  protected readonly data = inject<RotateKeyConfirmDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<RotateKeyConfirmDialogComponent, boolean>>(MatDialogRef);

  confirm(): void {
    this.dialogRef.close(true);
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}
