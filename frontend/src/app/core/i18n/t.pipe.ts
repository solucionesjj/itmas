import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18nService, MessageParams } from './i18n.service';
import { MessageKey } from './messages.es-CO';

/**
 * `{{ 'devices.create' | t }}`, or `{{ 'users.editFor' | t: { user: name } }}`.
 *
 * Deliberately **impure**. A pure pipe caches by its arguments, so it would never
 * re-run when the locale signal changes — the key is the same string, so Angular
 * would keep serving the previous language's text. `transform` is a Map lookup,
 * so running it per change-detection pass is cheap; this is the same trade-off
 * translation libraries make for their own pipes.
 */
@Pipe({ name: 't', standalone: true, pure: false })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(I18nService);

  transform(key: MessageKey, params?: MessageParams): string {
    return this.i18n.translate(key, params);
  }
}
