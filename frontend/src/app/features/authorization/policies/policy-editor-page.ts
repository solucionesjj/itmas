import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router, RouterLink } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../../auth/services/auth-service';
import { ApiError } from '../../../core/models/api-error';
import { problemMessage } from '../../../core/models/problem-message';
import { NotificationService } from '../../../core/services/notification-service';
import { AuthorizationApi } from '../data/authorization-api';
import { Permission } from '../data/authorization.models';
import { groupByCategory } from '../data/permission-groups';

/** Create (`/admin/policies/new`) or edit (`/admin/policies/:id/edit`) a policy. */
@Component({
  selector: 'app-policy-editor-page',
  imports: [
    MatButtonModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSlideToggleModule,
    MatTooltipModule,
    ReactiveFormsModule,
    RouterLink,
  ],
  templateUrl: './policy-editor-page.html',
  styleUrls: ['../admin-page.scss', './policy-editor-page.scss'],
})
export class PolicyEditorPage {
  private readonly api = inject(AuthorizationApi);
  private readonly router = inject(Router);
  private readonly notifications = inject(NotificationService);
  private readonly auth = inject(AuthService);

  /** Route parameter; absent when creating. */
  readonly id = input<string>();

  protected readonly form = new FormGroup({
    name: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.maxLength(100)],
    }),
    description: new FormControl('', {
      nonNullable: true,
      validators: [Validators.maxLength(500)],
    }),
    isActive: new FormControl(true, { nonNullable: true }),
  });

  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly canReadCatalog = this.auth.hasPermission('permissions.read');
  protected readonly selected = signal<ReadonlySet<string>>(new Set());
  protected readonly removedObsolete = signal<readonly string[]>([]);
  private readonly catalog = signal<readonly Permission[]>([]);
  private etag = '';

  /** Only grantable, non-deprecated permissions can be part of a policy. */
  protected readonly groups = computed(() =>
    groupByCategory(
      this.catalog().filter((permission) => permission.isGrantable && !permission.isDeprecated),
    ),
  );

  constructor() {
    effect(() => this.load(this.id()));
  }

  protected isEditing(): boolean {
    return !!this.id();
  }

  /** Delegated administrators may only grant permissions they hold (SPEC-0001 RN-10); already-present ones stay editable. */
  protected canGrant(permission: string): boolean {
    return this.auth.hasPermission(permission) || this.selected().has(permission);
  }

  protected toggle(permission: string, checked: boolean): void {
    const next = new Set(this.selected());
    if (checked) {
      next.add(permission);
    } else {
      next.delete(permission);
    }
    this.selected.set(next);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const input = {
      name: value.name.trim(),
      description: value.description.trim(),
      permissions: [...this.selected()],
    };
    const id = this.id();
    this.saving.set(true);

    const request = id
      ? this.api.updatePolicy(id, this.etag, { ...input, isActive: value.isActive })
      : this.api.createPolicy(input);

    request.subscribe({
      next: (policy) => {
        this.saving.set(false);
        this.notifications.info(id ? 'Política actualizada.' : 'Política creada.');
        void this.router.navigate(['/admin/policies', policy.id]);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.notifications.error(
          error instanceof ApiError && error.status === 412
            ? 'Otra persona modificó la política mientras la editabas. Recarga la página para ver los cambios.'
            : problemMessage(error),
        );
      },
    });
  }

  private load(id: string | undefined): void {
    this.loading.set(true);
    const catalog$ = this.canReadCatalog
      ? this.api.getPermissions()
      : of([] as readonly Permission[]);
    catalog$.subscribe({
      next: (catalog) => {
        this.catalog.set(catalog);
        if (!id) {
          this.loading.set(false);
          return;
        }
        this.api.getPolicy(id).subscribe({
          next: ({ policy, etag }) => {
            this.etag = etag;
            this.form.setValue({
              name: policy.name,
              description: policy.description,
              isActive: policy.isActive,
            });
            const assignable = new Set(
              catalog
                .filter((permission) => permission.isGrantable && !permission.isDeprecated)
                .map((p) => p.name),
            );
            this.selected.set(new Set(policy.permissions.filter((name) => assignable.has(name))));
            this.removedObsolete.set(policy.permissions.filter((name) => !assignable.has(name)));
            this.loading.set(false);
          },
          error: () => this.loading.set(false),
        });
      },
      error: () => this.loading.set(false),
    });
  }
}
