import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';

/** Root component: only hosts the router. Layout lives in layout/, features in features/. */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  template: '<router-outlet />',
  styles: `
    :host {
      display: block;
      height: 100%;
    }
  `,
})
export class App {}
