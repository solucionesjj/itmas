import { DOCUMENT, Injectable, inject } from '@angular/core';

/**
 * Full-page navigations that leave the SPA (sign-in / sign-out with Microsoft Entra ID through the BFF).
 * Isolated in a service so that it can be replaced in tests.
 */
@Injectable({ providedIn: 'root' })
export class BrowserNavigation {
  private readonly document = inject(DOCUMENT);

  assign(url: string): void {
    this.document.location.assign(url);
  }

  /** Same-origin form POST (top-level navigation), e.g. for sign-out. */
  postForm(action: string): void {
    const form = this.document.createElement('form');
    form.method = 'POST';
    form.action = action;
    form.hidden = true;
    this.document.body.appendChild(form);
    form.submit();
  }
}
