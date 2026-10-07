import { Injectable, inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';

/** User-facing notifications (Material snack bar). Messages must never contain technical details. */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  info(message: string): void {
    this.snackBar.open(message, 'Cerrar', { duration: 4000 });
  }

  error(message: string): void {
    this.snackBar.open(message, 'Cerrar', { duration: 8000, politeness: 'assertive' });
  }
}
