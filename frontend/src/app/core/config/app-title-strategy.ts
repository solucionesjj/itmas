import { Injectable, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';
import { APP_CONFIG } from './app-config';

/** Document title: "<route title> · <app name>". */
@Injectable()
export class AppTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);
  private readonly config = inject(APP_CONFIG);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    const routeTitle = this.buildTitle(snapshot);
    this.title.setTitle(
      routeTitle ? `${routeTitle} · ${this.config.appName}` : this.config.appName,
    );
  }
}
