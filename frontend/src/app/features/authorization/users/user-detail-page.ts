import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Observable, catchError, filter, finalize, forkJoin, map, of, switchMap } from 'rxjs';
import { IfPermission } from '../../../auth/directives/if-permission';
import { AuthService } from '../../../auth/services/auth-service';
import { problemMessage } from '../../../core/models/problem-message';
import { NotificationService } from '../../../core/services/notification-service';
import { confirm } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { AuthorizationApi } from '../data/authorization-api';
import {
  AssignedPolicy,
  EffectivePermissions,
  Permission,
  UserDetail,
} from '../data/authorization.models';
import { AssignPolicyDialog, AssignPolicyDialogData } from './assign-policy-dialog';

@Component({
  selector: 'app-user-detail-page',
  imports: [
    DatePipe,
    IfPermission,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatIconModule,
    MatListModule,
    MatProgressBarModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
    RouterLink,
  ],
  templateUrl: './user-detail-page.html',
  styleUrl: '../admin-page.scss',
})
export class UserDetailPage {
  private readonly api = inject(AuthorizationApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifications = inject(NotificationService);
  protected readonly auth = inject(AuthService);

  /** Route parameter (component input binding). */
  readonly id = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly user = signal<UserDetail | null>(null);
  protected readonly effective = signal<EffectivePermissions | null>(null);
  private readonly catalog = signal<ReadonlyMap<string, Permission>>(new Map());

  protected readonly permissionColumns = ['permission', 'grantedBy'];

  constructor() {
    effect(() => this.load(this.id()));
    if (this.auth.hasPermission('permissions.read')) {
      this.api
        .getPermissions()
        .subscribe((permissions) =>
          this.catalog.set(new Map(permissions.map((permission) => [permission.name, permission]))),
        );
    }
  }

  protected displayName(permission: string): string {
    return this.catalog().get(permission)?.displayName ?? '';
  }

  protected toggleActive(user: UserDetail): void {
    const activate = !user.isActive;
    this.confirmAndRun(
      {
        title: activate ? 'Activar usuario' : 'Desactivar usuario',
        message: activate
          ? `${user.displayName} podrá volver a iniciar sesión con las políticas que tenga asignadas.`
          : `${user.displayName} no podrá iniciar sesión y sus sesiones abiertas terminarán de inmediato.`,
        confirmLabel: activate ? 'Activar' : 'Desactivar',
      },
      () => this.api.setUserActive(user.id, activate),
      activate ? 'Usuario activado.' : 'Usuario desactivado.',
    );
  }

  protected toggleRoot(user: UserDetail): void {
    const grant = !user.isRoot;
    this.confirmAndRun(
      {
        title: grant ? 'Conceder Root' : 'Revocar Root',
        message: grant
          ? `${user.displayName} tendrá acceso irrestricto a toda la aplicación y a la administración de autorización.`
          : `${user.displayName} dejará de ser Root y solo conservará los permisos de sus políticas.`,
        confirmLabel: grant ? 'Conceder Root' : 'Revocar Root',
      },
      () => this.api.setRoot(user.id, grant),
      grant ? 'Root concedido.' : 'Root revocado.',
    );
  }

  protected assignPolicy(user: UserDetail): void {
    this.dialog
      .open<AssignPolicyDialog, AssignPolicyDialogData, string>(AssignPolicyDialog, {
        data: { assignedPolicyIds: user.policies.map((policy) => policy.id) },
      })
      .afterClosed()
      .pipe(
        filter((policyId): policyId is string => !!policyId),
        switchMap((policyId) => this.run(() => this.api.assignPolicy(user.id, policyId))),
      )
      .subscribe((ok) => ok && this.done('Política asignada.'));
  }

  protected revokePolicy(user: UserDetail, policy: AssignedPolicy): void {
    this.confirmAndRun(
      {
        title: 'Retirar política',
        message: `${user.displayName} perderá los permisos que solo concede "${policy.name}".`,
        confirmLabel: 'Retirar',
      },
      () => this.api.revokePolicy(user.id, policy.id),
      'Política retirada.',
    );
  }

  private load(id: string): void {
    this.loading.set(true);
    forkJoin([this.api.getUser(id), this.api.getEffectivePermissions(id)]).subscribe({
      next: ([user, effective]) => {
        this.user.set(user);
        this.effective.set(effective);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private confirmAndRun(
    dialog: { title: string; message: string; confirmLabel: string },
    action: () => Observable<void>,
    successMessage: string,
  ): void {
    confirm(this.dialog, dialog)
      .pipe(
        filter((confirmed) => !!confirmed),
        switchMap(() => this.run(action)),
      )
      .subscribe((ok) => ok && this.done(successMessage));
  }

  /** Runs a mutation; API errors (403/409/422…) are reported with their Problem Details message. */
  private run(action: () => Observable<void>): Observable<boolean> {
    this.busy.set(true);
    return action().pipe(
      map(() => true),
      catchError((error: unknown) => {
        this.notifications.error(problemMessage(error));
        return of(false);
      }),
      finalize(() => this.busy.set(false)),
    );
  }

  private done(message: string): void {
    this.notifications.info(message);
    this.load(this.id());
  }
}
