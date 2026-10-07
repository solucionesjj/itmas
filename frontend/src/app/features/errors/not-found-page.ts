import { Component } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { StatusMessage } from '../../shared/components/status-message/status-message';

@Component({
  selector: 'app-not-found-page',
  imports: [MatButton, RouterLink, StatusMessage],
  template: `
    <app-status-message
      icon="search_off"
      title="Página no encontrada"
      description="La página que buscas no existe o fue movida."
    >
      <a actions mat-button routerLink="/">Volver al inicio</a>
    </app-status-message>
  `,
  styles: `
    :host {
      display: block;
      height: 100%;
    }
  `,
})
export class NotFoundPage {}
