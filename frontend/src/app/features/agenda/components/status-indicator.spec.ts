import { TestBed } from '@angular/core/testing';
import { WarningList } from './warning-list';
import { EventStatusChips } from './event-status-chips';
import { OutlookStatusIndicator } from './outlook-status-indicator';
import { EventStatus, OutlookStatus } from '../data/agenda.models';

const text = (node: Element | null | undefined) =>
  node?.textContent?.replace(/\s+/g, ' ').trim() ?? '';

describe('EventStatusChips (RNF-2, RF-8)', () => {
  const render = (status: EventStatus, overdue = false) => {
    const fixture = TestBed.createComponent(EventStatusChips);
    fixture.componentRef.setInput('status', status);
    fixture.componentRef.setInput('overdue', overdue);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  };

  it.each([
    ['draft', 'edit_note', 'Borrador'],
    ['scheduled', 'event_available', 'Programada'],
    ['completed', 'check_circle', 'Completada'],
    ['cancelled', 'cancel', 'Cancelada'],
  ] as const)(
    'draws %s with an icon and its text, never with colour alone',
    (status, icon, label) => {
      const element = render(status);

      expect(text(element.querySelector('mat-icon'))).toBe(icon);
      expect(text(element.querySelector('app-agenda-status span'))).toBe(label);
      expect(element.querySelector('mat-chip-set')?.getAttribute('aria-label')).toBe(
        `Estado: ${label}`,
      );
    },
  );

  it('draws Vencida as a second chip next to the state, not instead of it', () => {
    const element = render('scheduled', true);
    const chips = [...element.querySelectorAll('mat-chip')].map((chip) => ({
      icon: text(chip.querySelector('mat-icon')),
      label: text(chip.querySelector('app-agenda-status span')),
    }));

    expect(chips).toEqual([
      { icon: 'event_available', label: 'Programada' },
      { icon: 'warning', label: 'Vencida' },
    ]);
    expect(element.querySelector('mat-chip-set')?.getAttribute('aria-label')).toBe(
      'Estado: Programada, Vencida',
    );
  });
});

describe('OutlookStatusIndicator (RF-18)', () => {
  it.each([
    ['not-published', 'No publicado'],
    ['pending', 'Pendiente'],
    ['published', 'Publicado'],
    ['error', 'Error'],
  ] as const)('draws %s with an icon and the text "%s"', (status: OutlookStatus, label) => {
    const fixture = TestBed.createComponent(OutlookStatusIndicator);
    fixture.componentRef.setInput('status', status);
    fixture.detectChanges();

    expect(text(fixture.nativeElement.querySelector('span'))).toBe(label);
    expect(text(fixture.nativeElement.querySelector('mat-icon'))).toBeTruthy();
  });
});

describe('WarningList (RN-11, CA-11)', () => {
  it('draws nothing without warnings', () => {
    const fixture = TestBed.createComponent(WarningList);
    fixture.componentRef.setInput('warnings', []);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('ul')).toBeNull();
  });

  it('draws each warning with an icon and its text, as a status that does not block', () => {
    const fixture = TestBed.createComponent(WarningList);
    fixture.componentRef.setInput('warnings', [
      { kind: 'freeze-overlap', eventId: 'f1', title: 'Cierre contable' },
      { kind: 'impact-overlap', eventId: 'e2', title: 'Migración <b>de red</b>' },
    ]);
    fixture.detectChanges();
    const list = fixture.nativeElement.querySelector('ul') as HTMLElement;

    expect(list.getAttribute('role')).toBe('status');
    expect([...list.querySelectorAll('li span')].map((span) => text(span))).toEqual([
      'Esta actividad se solapa con el congelamiento «Cierre contable».',
      'Se solapa con «Migración <b>de red</b>», que también tiene impacto.',
    ]);
    expect(list.querySelectorAll('mat-icon').length).toBe(2);
    expect(list.querySelector('b')).toBeNull();
  });
});
