import { booleanAttribute, Component, input } from '@angular/core';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { Logo } from '../logo/logo';

/**
 * Centered message with an icon, a title, a description and projected actions, on an opaque card (SPEC-0010 RF-9).
 * Reused by sign-in and error pages. With `brand`, the title is the ITMas logo (RF-10) instead of the icon and the text.
 */
@Component({
  selector: 'app-status-message',
  imports: [MatCard, MatCardContent, MatIcon, Logo],
  template: `
    <mat-card appearance="outlined" class="card itmas-opaque-card">
      <mat-card-content class="body">
        @if (brand()) {
          <h1 class="title brand"><app-logo /></h1>
        } @else {
          <mat-icon class="icon" aria-hidden="true">{{ icon() }}</mat-icon>
          <h1 class="title">{{ title() }}</h1>
        }
        <p class="description">{{ description() }}</p>
        <ng-content />
        <div class="actions"><ng-content select="[actions]" /></div>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    :host {
      display: grid;
      place-items: center;
      min-height: 100%;
      padding: 24px;
      box-sizing: border-box;
    }
    .card {
      width: min(440px, 100%);
    }
    .body {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: 8px;
      padding: 24px;
    }
    .icon {
      font-size: var(--itmas-icon-size-lg);
      width: var(--itmas-icon-size-lg);
      height: var(--itmas-icon-size-lg);
      color: var(--mat-sys-primary);
    }
    .title {
      font: var(--mat-sys-headline-small);
      margin: 8px 0 0;
    }
    .brand {
      margin: 8px 0;
    }
    .description {
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
      margin: 0 0 8px;
    }
    .actions {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: 8px;
      margin-top: 8px;
    }
  `,
})
export class StatusMessage {
  readonly icon = input.required<string>();
  readonly title = input.required<string>();
  readonly description = input<string>('');
  /** Shows the logo as the title (sign-in page); `title` must then be the application name, which is the logo's text. */
  readonly brand = input(false, { transform: booleanAttribute });
}
