import { Component, inject } from '@angular/core';
import { MatIcon } from '@angular/material/icon';
import { APP_CONFIG } from '../../../core/config/app-config';

/**
 * Typographic logo of ITMas (SPEC-0010 RF-10): an icon in a box, the name in Poppins 600 and an orange dot as a detail. Template
 * only (no remote SVG, no injected markup); its look lives in the theme layer (`.itmas-logo*`). Its accessible text is the name alone.
 */
@Component({
  selector: 'app-logo',
  imports: [MatIcon],
  host: { class: 'itmas-logo' },
  template: `
    <span class="itmas-logo-mark" aria-hidden="true"><mat-icon>hub</mat-icon></span>
    <span>{{ appName }}<span class="itmas-logo-dot" aria-hidden="true"></span></span>
  `,
})
export class Logo {
  protected readonly appName = inject(APP_CONFIG).appName;
}
