import { Component, inject } from '@angular/core';
import { ClipboardModule } from '@angular/cdk/clipboard';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';

export interface ApiKeyRevealDialogData {
  hostname: string;
  deviceId: string;
  apiKey: string;
}

// One-time reveal only: this dialog's `apiKey` comes straight from a
// POST /devices or POST /devices/:id/rotate-key response body — the only
// two places the backend ever returns it in plaintext. Once this dialog is
// closed there is no way to see this value again (only its argon2 hash is
// stored server-side), so the copy affordance below is the user's one shot.
@Component({
  selector: 'app-api-key-reveal-dialog',
  standalone: true,
  imports: [MatDialogModule, MatButtonModule, MatIconModule, ClipboardModule],
  templateUrl: './api-key-reveal-dialog.component.html',
  styleUrl: './api-key-reveal-dialog.component.scss'
})
export class ApiKeyRevealDialogComponent {
  protected readonly data = inject<ApiKeyRevealDialogData>(MAT_DIALOG_DATA);
  private readonly snackBar = inject(MatSnackBar);

  protected onCopied(success: boolean): void {
    this.snackBar.open(
      success ? 'Clave copiada al portapapeles.' : 'No se pudo copiar la clave.',
      'Cerrar',
      { duration: 3000 }
    );
  }
}
