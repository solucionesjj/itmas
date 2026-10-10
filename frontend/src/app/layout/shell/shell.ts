import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import {
  MatListItem,
  MatListItemIcon,
  MatListItemTitle,
  MatListSubheaderCssMatStyler,
  MatNavList,
} from '@angular/material/list';
import { MatSidenav, MatSidenavContainer, MatSidenavContent } from '@angular/material/sidenav';
import { MatToolbar } from '@angular/material/toolbar';
import { MatTooltip } from '@angular/material/tooltip';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../auth/services/auth-service';
import { APP_CONFIG } from '../../core/config/app-config';
import { NAVIGATION } from '../navigation/navigation-items';
import { ThemeToggle } from '../theme-toggle/theme-toggle';
import { UserMenu } from '../user-menu/user-menu';

/** Authenticated application frame: top app bar, navigation drawer and content outlet. */
@Component({
  selector: 'app-shell',
  imports: [
    MatToolbar,
    MatTooltip,
    MatIconButton,
    MatIcon,
    MatSidenav,
    MatSidenavContainer,
    MatSidenavContent,
    MatNavList,
    MatListItem,
    MatListItemIcon,
    MatListItemTitle,
    MatListSubheaderCssMatStyler,
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    ThemeToggle,
    UserMenu,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  protected readonly appName = inject(APP_CONFIG).appName;
  private readonly auth = inject(AuthService);

  /** Navigation filtered by the user's permissions; empty sections are hidden. */
  protected readonly navigation = computed(() => {
    this.auth.session();
    return NAVIGATION.map((group) => ({
      ...group,
      items: group.items.filter(
        (item) => !item.permission || this.auth.hasPermission(item.permission),
      ),
    })).filter((group) => group.items.length > 0);
  });

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe(Breakpoints.Handset)
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );
}
