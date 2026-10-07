import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { filter, switchMap } from 'rxjs';
import { IfPermission } from '../../../auth/directives/if-permission';
import { problemMessage } from '../../../core/models/problem-message';
import { NotificationService } from '../../../core/services/notification-service';
import { AuthorizationApi } from '../data/authorization-api';
import { Permission, PermissionMetadata } from '../data/authorization.models';
import { EditPermissionDialog } from './edit-permission-dialog';

/** Permission catalog. Permissions are declared in code; only their presentation metadata is editable. */
@Component({
  selector: 'app-permissions-page',
  imports: [
    IfPermission,
    MatButtonModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: './permissions-page.html',
  styleUrl: '../admin-page.scss',
})
export class PermissionsPage {
  private readonly api = inject(AuthorizationApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);

  protected readonly columns = ['name', 'category', 'status', 'policyCount', 'actions'];
  protected readonly loading = signal(true);
  protected readonly text = signal('');
  protected readonly category = signal('');
  private readonly permissions = signal<readonly Permission[]>([]);

  protected readonly categories = computed(() =>
    [...new Set(this.permissions().map((permission) => permission.category))].sort((a, b) =>
      a.localeCompare(b, 'es'),
    ),
  );

  protected readonly filtered = computed(() => {
    const text = this.text().toLocaleLowerCase('es');
    return this.permissions().filter(
      (permission) =>
        (!this.category() || permission.category === this.category()) &&
        (!text ||
          permission.name.includes(text) ||
          permission.displayName.toLocaleLowerCase('es').includes(text) ||
          permission.description.toLocaleLowerCase('es').includes(text)),
    );
  });

  constructor() {
    this.load();
  }

  protected edit(permission: Permission): void {
    this.dialog
      .open<EditPermissionDialog, Permission, PermissionMetadata>(EditPermissionDialog, {
        data: permission,
      })
      .afterClosed()
      .pipe(
        filter((metadata): metadata is PermissionMetadata => !!metadata),
        switchMap((metadata) => this.api.updatePermission(permission.name, metadata)),
      )
      .subscribe({
        next: () => {
          this.notifications.info('Permiso actualizado.');
          this.load();
        },
        error: (error: unknown) => this.notifications.error(problemMessage(error)),
      });
  }

  private load(): void {
    this.loading.set(true);
    this.api.getPermissions().subscribe({
      next: (permissions) => {
        this.permissions.set(permissions);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}
