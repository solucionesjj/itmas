import { Component, input } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { statusLabel, typeLabel } from '../data/inventory-labels';
import { DeviceHeader } from '../data/inventory.models';

/** Device header of the history and report pages: name, type, status and associated collaborator (RF-17). */
@Component({
  selector: 'app-device-header',
  imports: [MatButtonModule, MatChipsModule, MatIconModule, RouterLink],
  template: `
    <div class="page-header">
      <a mat-icon-button routerLink="/inventory" aria-label="Volver a equipos">
        <mat-icon>arrow_back</mat-icon>
      </a>
      <h1 class="page-title">{{ device()?.name ?? fallbackTitle() }}</h1>
      @if (device(); as device) {
        <mat-chip-set aria-label="Tipo y estado">
          <mat-chip [disableRipple]="true">{{ typeLabel(device.type) }}</mat-chip>
          <mat-chip [disableRipple]="true" [highlighted]="device.status !== 'active'">
            {{ statusLabel(device.status) }}
          </mat-chip>
        </mat-chip-set>
      }
      <ng-content />
    </div>
    @if (device(); as device) {
      <p class="owner">
        <mat-icon aria-hidden="true">person</mat-icon>
        @if (device.owner; as owner) {
          <span>
            @if (owner.name) {
              {{ owner.name }} ·
            }
            {{ owner.email }}
          </span>
        } @else {
          <span>Sin usuario asociado</span>
        }
      </p>
    }
  `,
  styles: `
    .page-header {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 8px 16px;
    }
    .page-title {
      font: var(--mat-sys-headline-small);
      margin: 0;
      flex: 1 1 auto;
      overflow-wrap: anywhere;
    }
    .owner {
      display: flex;
      align-items: center;
      gap: 8px;
      font: var(--mat-sys-body-large);
      color: var(--mat-sys-on-surface-variant);
      overflow-wrap: anywhere;
    }
  `,
})
export class DeviceHeaderComponent {
  readonly device = input<DeviceHeader | null>(null);
  readonly fallbackTitle = input('Equipo');

  protected readonly typeLabel = typeLabel;
  protected readonly statusLabel = statusLabel;
}
