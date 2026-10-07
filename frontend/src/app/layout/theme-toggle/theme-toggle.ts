import { Component, computed, inject } from '@angular/core';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MatMenu, MatMenuItem, MatMenuTrigger } from '@angular/material/menu';
import { MatTooltip } from '@angular/material/tooltip';
import { ThemePreference, ThemeService } from '../../core/services/theme-service';

interface ThemeOption {
  readonly value: ThemePreference;
  readonly label: string;
  readonly icon: string;
}

@Component({
  selector: 'app-theme-toggle',
  imports: [MatIconButton, MatIcon, MatMenu, MatMenuItem, MatMenuTrigger, MatTooltip],
  template: `
    <button
      mat-icon-button
      [matMenuTriggerFor]="themeMenu"
      matTooltip="Tema"
      aria-label="Cambiar tema"
    >
      <mat-icon>{{ currentOption().icon }}</mat-icon>
    </button>
    <mat-menu #themeMenu="matMenu">
      @for (option of options; track option.value) {
        <button
          mat-menu-item
          role="menuitemradio"
          [attr.aria-checked]="option.value === theme.preference()"
          (click)="theme.setPreference(option.value)"
        >
          <mat-icon>{{ option.value === theme.preference() ? 'check' : option.icon }}</mat-icon>
          <span>{{ option.label }}</span>
        </button>
      }
    </mat-menu>
  `,
})
export class ThemeToggle {
  protected readonly theme = inject(ThemeService);

  protected readonly options: readonly ThemeOption[] = [
    { value: 'light', label: 'Claro', icon: 'light_mode' },
    { value: 'dark', label: 'Oscuro', icon: 'dark_mode' },
    { value: 'system', label: 'Según el sistema', icon: 'brightness_auto' },
  ];

  protected readonly currentOption = computed(
    () =>
      this.options.find((option) => option.value === this.theme.preference()) ?? this.options[2],
  );
}
