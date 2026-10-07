import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { filter, forkJoin, of, switchMap } from 'rxjs';
import { IfPermission } from '../../../auth/directives/if-permission';
import { AuthService } from '../../../auth/services/auth-service';
import { problemMessage } from '../../../core/models/problem-message';
import { NotificationService } from '../../../core/services/notification-service';
import { confirm } from '../../../shared/components/confirm-dialog/confirm-dialog';
import { AuthorizationApi } from '../data/authorization-api';
import { Permission, PolicyDetail, UserReference } from '../data/authorization.models';
import { groupByCategory } from '../data/permission-groups';

@Component({
  selector: 'app-policy-detail-page',
  imports: [
    DatePipe,
    IfPermission,
    MatButtonModule,
    MatChipsModule,
    MatIconModule,
    MatListModule,
    MatProgressBarModule,
    RouterLink,
  ],
  templateUrl: './policy-detail-page.html',
  styleUrl: '../admin-page.scss',
})
export class PolicyDetailPage {
  private readonly api = inject(AuthorizationApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();

  protected readonly loading = signal(true);
  protected readonly policy = signal<PolicyDetail | null>(null);
  protected readonly users = signal<readonly UserReference[]>([]);
  private readonly catalog = signal<readonly Permission[] | null>(null);

  /** Permissions of the policy grouped by category (falls back to the raw names without catalog access). */
  protected readonly groups = computed(() => {
    const policy = this.policy();
    if (!policy) {
      return [];
    }
    const catalog = this.catalog();
    if (!catalog) {
      return [
        {
          category: 'Permisos',
          permissions: policy.permissions.map((name) => unknownPermission(name)),
        },
      ];
    }
    const byName = new Map(catalog.map((permission) => [permission.name, permission]));
    return groupByCategory(
      policy.permissions.map((name) => byName.get(name) ?? unknownPermission(name)),
    );
  });

  constructor() {
    effect(() => this.load(this.id()));
  }

  protected delete(policy: PolicyDetail): void {
    confirm(this.dialog, {
      title: 'Eliminar política',
      message: `Se eliminará "${policy.name}". Esta acción no se puede deshacer.`,
      confirmLabel: 'Eliminar',
    })
      .pipe(
        filter((confirmed) => !!confirmed),
        switchMap(() => this.api.deletePolicy(policy.id)),
      )
      .subscribe({
        next: () => {
          this.notifications.info('Política eliminada.');
          void this.router.navigate(['/admin/policies']);
        },
        error: (error: unknown) => this.notifications.error(problemMessage(error)),
      });
  }

  private load(id: string): void {
    this.loading.set(true);
    forkJoin([
      this.api.getPolicy(id),
      this.api.getPolicyUsers(id),
      this.auth.hasPermission('permissions.read') ? this.api.getPermissions() : of(null),
    ]).subscribe({
      next: ([versioned, users, catalog]) => {
        this.policy.set(versioned.policy);
        this.users.set(users);
        this.catalog.set(catalog);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }
}

function unknownPermission(name: string): Permission {
  return {
    name,
    displayName: '',
    description: '',
    category: 'Permisos',
    isGrantable: true,
    isDeprecated: false,
    policyCount: 0,
  };
}
