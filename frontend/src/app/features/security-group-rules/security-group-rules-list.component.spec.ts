import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { SecurityGroupRulesListComponent } from './security-group-rules-list.component';
import { SecurityGroupRulesService } from './security-group-rules.service';
import { SecurityGroupSyncService } from './security-group-sync.service';

describe('SecurityGroupRulesListComponent', () => {
  let rulesServiceSpy: jasmine.SpyObj<SecurityGroupRulesService>;
  let syncServiceSpy: jasmine.SpyObj<SecurityGroupSyncService>;

  function configure(role: 'administrator' | 'user' | 'auditor' = 'administrator') {
    rulesServiceSpy = jasmine.createSpyObj('SecurityGroupRulesService', [
      'list',
      'listGroups',
      'review',
      'authorize',
      'export'
    ]);
    rulesServiceSpy.list.and.returnValue(of({ items: [], total: 0, page: 1, limit: 20 }));
    rulesServiceSpy.listGroups.and.returnValue(of([]));
    syncServiceSpy = jasmine.createSpyObj('SecurityGroupSyncService', ['run']);

    TestBed.configureTestingModule({
      imports: [SecurityGroupRulesListComponent],
      providers: [
        provideAnimationsAsync(),
        // The view reflects its nine filters into the URL (§10.1), so it injects
        // Router/ActivatedRoute.
        provideRouter([]),
        { provide: SecurityGroupRulesService, useValue: rulesServiceSpy },
        { provide: SecurityGroupSyncService, useValue: syncServiceSpy },
        {
          provide: AuthService,
          useValue: {
            currentUser: () => ({
              sub: 'user-1',
              username: 'tester',
              role,
              mustChangePassword: false,
              iat: 0,
              exp: 0
            })
          }
        },
        { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) }
      ]
    });
  }

  // Regression: this view used to reflect its filters into the URL from `reload()`,
  // and the first `reload()` runs in the constructor — i.e. during the very
  // navigation that is activating this route. That `router.navigate()` superseded
  // the in-flight navigation, so it ended as NavigationCancel and the router never
  // emitted NavigationEnd; the sidebar (`routerLinkActive`) and the shell's toolbar
  // title both listen for NavigationEnd only, so selecting "Reglas de Firewall AWS"
  // left the previous item highlighted. See syncFiltersToUrl in filter-url.util.ts.
  it('does not touch the URL while it is being routed to', () => {
    configure();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    TestBed.createComponent(SecurityGroupRulesListComponent);

    expect(rulesServiceSpy.list).toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('writes the filters to the URL once a filter changes', fakeAsync(() => {
    configure();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(SecurityGroupRulesListComponent);

    fixture.componentInstance['filters'].patchValue({ status: 'revisado' });
    tick(300);

    expect(navigate).toHaveBeenCalledTimes(1);
    const [commands, extras] = navigate.calls.mostRecent().args as [
      unknown[],
      { queryParams: Record<string, string | undefined>; replaceUrl: boolean }
    ];
    expect(commands).toEqual([]);
    expect(extras.queryParams['status']).toBe('revisado');
    expect(extras.queryParams['q']).toBeUndefined();
    expect(extras.replaceUrl).toBe(true);
  }));
});
