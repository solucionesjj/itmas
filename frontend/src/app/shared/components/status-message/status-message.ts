import { Component, input } from '@angular/core';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';

/**
 * Centered message with an icon, a title, a description and projected actions.
 * Reused by sign-in and error pages.
 */
@Component({
  selector: 'app-status-message',
  imports: [MatCard, MatCardContent, MatIcon],
  template: `
    <mat-card appearance="outlined" class="card">
      <mat-card-content class="body">
        <mat-icon class="icon" aria-hidden="true">{{ icon() }}</mat-icon>
        <h1 class="title">{{ title() }}</h1>
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
      font-size: 48px;
      width: 48px;
      height: 48px;
      color: var(--mat-sys-primary);
    }
    .title {
      font: var(--mat-sys-headline-small);
      margin: 8px 0 0;
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
}
