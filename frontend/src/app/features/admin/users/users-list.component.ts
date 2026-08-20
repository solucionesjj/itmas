import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { AuthService } from '../../../core/services/auth.service';
import { I18nService } from '../../../core/i18n/i18n.service';
import { TranslatePipe } from '../../../core/i18n/t.pipe';
import { ViewError, toViewError } from '../../../core/utils/api-error.util';
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
  imports: [
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatProgressBarModule,
    TranslatePipe
  ],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss'
})
export class UsersListComponent {
  private readonly usersService = inject(UsersService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  protected readonly i18n = inject(I18nService);

  protected readonly displayedColumns = [
    'username',
    'email',
    'role',
    'active',
    'actions'
  ];
  protected readonly users = signal<User[]>([]);
  protected readonly error = signal<ViewError | null>(null);

  // §10.4: skeletons on the first load, a 2px bar over stale rows on a refresh.
  protected readonly loading = signal(false);
  protected readonly firstLoad = signal(true);
  protected readonly showSkeletons = computed(() => this.loading() && this.firstLoad());
  protected readonly refreshing = computed(() => this.loading() && !this.firstLoad());

  constructor() {
    this.reload();
  }

  // This view has no filters, so it only ever needs the "no data yet" empty
  // state — there is no filtered-to-nothing case to distinguish.
  protected reload(): void {
    this.loading.set(true);
    this.error.set(null);
    this.usersService.list().subscribe({
      next: (users) => {
        this.users.set(users);
        this.loading.set(false);
        this.firstLoad.set(false);
      },
      error: (err) => {
        this.error.set(toViewError(err, this.i18n.translate('users.error')));
        this.loading.set(false);
        this.firstLoad.set(false);
      }
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
          this.snackBar.open(this.i18n.translate('users.created'), this.i18n.translate('action.close'), { duration: 3000 });
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
          this.snackBar.open(this.i18n.translate('users.updated'), this.i18n.translate('action.close'), { duration: 3000 });
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
          this.i18n.translate(user.active ? 'users.deactivated' : 'users.activated'),
          this.i18n.translate('action.close'),
          { duration: 3000 }
        );
        this.reload();
      },
      error: (err) => this.showError(err)
    });
  }

  private showError(err: unknown): void {
    this.snackBar.open(
      toViewError(err, this.i18n.translate('devices.operationFailed')).message,
      this.i18n.translate('action.close'),
      { duration: 4000 }
    );
  }
}
