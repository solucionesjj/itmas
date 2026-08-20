import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

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

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const page = this.buildTitle(snapshot);
    this.title.setTitle(page ? `IT-MAS · ${page}` : 'IT-MAS');
  }
}
