import { Directive, TemplateRef, ViewContainerRef, effect, inject, input } from '@angular/core';
import { AuthService } from '../services/auth-service';

/**
 * Renders its content only when the user holds the permission: `<button *appIfPermission="'users.update'">`.
 * UX only — never a security control.
 */
@Directive({ selector: '[appIfPermission]' })
export class IfPermission {
  private readonly auth = inject(AuthService);
  private readonly template = inject(TemplateRef<unknown>);
  private readonly container = inject(ViewContainerRef);

  readonly appIfPermission = input.required<string>();

  constructor() {
    effect(() => {
      const allowed = this.auth.hasPermission(this.appIfPermission());
      this.container.clear();
      if (allowed) {
        this.container.createEmbeddedView(this.template);
      }
    });
  }
}
