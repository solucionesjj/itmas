import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/services/auth.service';
import { UsersService } from './users.service';
import { User } from './user.model';
import {
  UserFormDialogComponent,
  UserFormDialogData,
  UserFormDialogResult
} from './user-form-dialog.component';

@Component({
  selector: 'app-users-list',
  standalone: true,
  imports: [MatTableModule, MatButtonModule, MatIconModule, MatDialogModule],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss'
})
export class UsersListComponent {
  private readonly usersService = inject(UsersService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly displayedColumns = [
    'username',
    'email',
    'role',
    'active',
    'actions'
  ];
  protected readonly users = signal<User[]>([]);
  protected readonly loading = signal(false);

  constructor() {
    this.reload();
  }

  private reload(): void {
    this.loading.set(true);
    this.usersService.list().subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
      },
      error: () => this.loading.set(false)
    });
  }

  protected isSelf(user: User): boolean {
    return this.authService.currentUser()?.sub === user._id;
  }

  protected openCreateDialog(): void {
    const ref = this.dialog.open<
      UserFormDialogComponent,
      UserFormDialogData,
      UserFormDialogResult
    >(UserFormDialogComponent, { data: { isSelf: false } });

    ref.afterClosed().subscribe((result) => {
      if (!result?.create) {
        return;
      }
      this.usersService.create(result.create).subscribe({
        next: () => {
          this.snackBar.open('Usuario creado.', 'Cerrar', { duration: 3000 });
          this.reload();
        },
        error: (err) => this.showError(err)
      });
    });
  }

  protected openEditDialog(user: User): void {
    const ref = this.dialog.open<
      UserFormDialogComponent,
      UserFormDialogData,
      UserFormDialogResult
    >(UserFormDialogComponent, { data: { user, isSelf: this.isSelf(user) } });

    ref.afterClosed().subscribe((result) => {
      if (!result?.update) {
        return;
      }
      this.usersService.update(user._id, result.update).subscribe({
        next: () => {
          this.snackBar.open('Usuario actualizado.', 'Cerrar', { duration: 3000 });
          this.reload();
        },
        error: (err) => this.showError(err)
      });
    });
  }

  protected toggleActive(user: User): void {
    this.usersService.update(user._id, { active: !user.active }).subscribe({
      next: () => {
        this.snackBar.open(
          user.active ? 'Usuario desactivado.' : 'Usuario activado.',
          'Cerrar',
          { duration: 3000 }
        );
        this.reload();
      },
      error: (err) => this.showError(err)
    });
  }

  private showError(err: { error?: { error?: { message?: string } } }): void {
    const message = err.error?.error?.message ?? 'No se pudo completar la operación.';
    this.snackBar.open(message, 'Cerrar', { duration: 4000 });
  }
}
