import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { I18nService } from '../i18n/i18n.service';
import { MessageKey } from '../i18n/messages.es-CO';

/**
 * Formats the document title as `IT-MAS · <page>` from each route's own `title`.
 *
 * Uses the router's native title mechanism (which drives Angular's `Title`
 * service, per design.md §7) rather than setting the title from the shell: the
 * shell is not instantiated for `login` / `change-password`, so a shell-owned
 * title left those two routes stuck on whatever the previous page had set.
 * The route's `title` stays the single source in `app.routes.ts`, and the shell
 * reads the same value for its toolbar.
 */
@Injectable({ providedIn: 'root' })
export class ItmasTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly i18n = inject(I18nService);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    // The route carries a message key, not a label, so the tab title changes with
    // the language along with everything else.
    const key = this.buildTitle(snapshot) as MessageKey | undefined;
    const product = this.i18n.translate('app.name');
    this.title.setTitle(key ? `${product} · ${this.i18n.translate(key)}` : product);
  }
}
