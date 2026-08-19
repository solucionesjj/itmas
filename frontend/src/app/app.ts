import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/services/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  // Injected at the root so the stored colour-scheme preference is applied on
  // every route, including `login` / `change-password`, which render outside the
  // shell (where the theme toggle itself lives). design.md §2.3.
  private readonly theme = inject(ThemeService);
}
