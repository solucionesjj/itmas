import { Component, computed, input } from '@angular/core';
import { outlookPresentation } from '../data/agenda-labels';
import { OutlookStatus } from '../data/agenda.models';
import { AgendaStatus } from './status-indicator';

/** Publication in Outlook (RF-18): icon and text, always. */
@Component({
  selector: 'app-outlook-status',
  imports: [AgendaStatus],
  template: `<app-agenda-status [status]="state()" />`,
})
export class OutlookStatusIndicator {
  readonly status = input.required<OutlookStatus>();

  protected readonly state = computed(() => outlookPresentation(this.status()));
}
