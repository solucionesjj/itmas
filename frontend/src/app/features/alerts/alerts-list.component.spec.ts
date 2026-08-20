import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { Router, provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AlertsListComponent } from './alerts-list.component';
import { AlertsService } from './alerts.service';

describe('AlertsListComponent', () => {
  let alertsServiceSpy: jasmine.SpyObj<AlertsService>;

  function configure() {
    alertsServiceSpy = jasmine.createSpyObj('AlertsService', ['list', 'updateStatus']);
    alertsServiceSpy.list.and.returnValue(of({ items: [], total: 0, page: 1, limit: 20 }));

    TestBed.configureTestingModule({
      imports: [AlertsListComponent],
      providers: [
        provideAnimationsAsync(),
        // The view reflects its filters into the URL (§10.1), so it injects
        // Router/ActivatedRoute.
        provideRouter([]),
        { provide: AlertsService, useValue: alertsServiceSpy },
        { provide: MatSnackBar, useValue: jasmine.createSpyObj('MatSnackBar', ['open']) }
      ]
    });
  }

  // Regression: see syncFiltersToUrl in filter-url.util.ts — navigating from the
  // constructor cancels the navigation that is activating this route, so the
  // router never emits NavigationEnd and the sidebar keeps the previous item
  // highlighted while this page is on screen.
  it('does not touch the URL while it is being routed to', () => {
    configure();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);

    TestBed.createComponent(AlertsListComponent);

    expect(alertsServiceSpy.list).toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('writes the filters to the URL once a filter changes', fakeAsync(() => {
    configure();
    const navigate = spyOn(TestBed.inject(Router), 'navigate').and.resolveTo(true);
    const fixture = TestBed.createComponent(AlertsListComponent);

    fixture.componentInstance['filters'].patchValue({ status: 'open' });
    tick(300);

    expect(navigate).toHaveBeenCalledTimes(1);
    const [commands, extras] = navigate.calls.mostRecent().args as [
      unknown[],
      { queryParams: Record<string, string | undefined>; replaceUrl: boolean }
    ];
    expect(commands).toEqual([]);
    expect(extras.queryParams['status']).toBe('open');
    expect(extras.queryParams['type']).toBeUndefined();
    expect(extras.replaceUrl).toBe(true);
  }));
});
