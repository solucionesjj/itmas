import { Component } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { StatusMessage } from '../../shared/components/status-message/status-message';

@Component({
  selector: 'app-forbidden-page',
  imports: [MatButton, RouterLink, StatusMessage],
  template: `
    <app-status-message
      icon="block"
      title="Acceso denegado"
      description="No tienes permisos para acceder a este recurso."
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
export class ForbiddenPage {}
