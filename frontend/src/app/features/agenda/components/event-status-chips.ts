import { Component, computed, input } from '@angular/core';
import { MatChipsModule } from '@angular/material/chips';
import { overduePresentation, statusPresentation } from '../data/agenda-labels';
import { EventStatus } from '../data/agenda.models';
import { AgendaStatus } from './status-indicator';

/** State of an activity as a chip; "Vencida" (a calculated mark, RF-8) goes next to it, never instead of it. */
@Component({
  selector: 'app-event-status',
  imports: [MatChipsModule, AgendaStatus],
  template: `
    <mat-chip-set [attr.aria-label]="'Estado: ' + text()">
      <mat-chip [disableRipple]="true">
        <app-agenda-status [status]="state()" />
      </mat-chip>
      @if (overdue()) {
        <mat-chip [disableRipple]="true">
          <app-agenda-status [status]="overdueState" />
        </mat-chip>
      }
    </mat-chip-set>
  `,
})
export class EventStatusChips {
  readonly status = input.required<EventStatus>();
  readonly overdue = input(false);

  protected readonly overdueState = overduePresentation();
  protected readonly state = computed(() => statusPresentation(this.status()));
  protected readonly text = computed(
    () => this.state().label + (this.overdue() ? ', ' + this.overdueState.label : ''),
  );
}
