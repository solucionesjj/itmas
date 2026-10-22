import { EventDetail, EventSummary, EventType, Summary, VersionedEvent } from './agenda.models';

/** Test data of the agenda feature (SPEC-0006 §12.1). Not used by the application. */

export const PATCHING_TYPE: EventType = {
  id: 't-patching',
  name: 'Mantenimiento y parches de sistema operativo',
  description: 'Parches mensuales',
  defaultDurationMinutes: 60,
  defaultReminderMinutes: 1440,
  isFreeze: false,
  isActive: true,
  revision: 1,
};

export const FREEZE_TYPE: EventType = {
  id: 't-freeze',
  name: 'Congelamiento de cambios',
  description: null,
  defaultDurationMinutes: 1440,
  defaultReminderMinutes: 1440,
  isFreeze: true,
  isActive: true,
  revision: 1,
};

export const TYPES: readonly EventType[] = [PATCHING_TYPE, FREEZE_TYPE];

export function summaryOf(overrides: Partial<EventSummary> = {}): EventSummary {
  return {
    id: 'e1',
    seriesId: null,
    type: { id: PATCHING_TYPE.id, name: PATCHING_TYPE.name, isFreeze: false },
    title: 'Parches de octubre',
    start: '2026-10-24T09:00:00',
    end: '2026-10-24T11:00:00',
    startUtc: '2026-10-24T14:00:00Z',
    endUtc: '2026-10-24T16:00:00Z',
    isAllDay: false,
    status: 'draft',
    isOverdue: false,
    responsible: { id: 'u1', name: 'Ana Pérez', isActive: true },
    impact: 'none',
    tags: [],
    hasWarnings: false,
    outlook: { status: 'not-published' },
    ...overrides,
  };
}

export function detailOf(overrides: Partial<EventDetail> = {}): EventDetail {
  return {
    id: 'e1',
    seriesId: null,
    series: null,
    type: { id: PATCHING_TYPE.id, name: PATCHING_TYPE.name, isFreeze: false },
    title: 'Parches de octubre',
    description: 'Aplicar los parches del mes',
    status: 'draft',
    isOverdue: false,
    isAllDay: false,
    start: '2026-10-24T09:00:00',
    end: '2026-10-24T11:00:00',
    startUtc: '2026-10-24T14:00:00Z',
    endUtc: '2026-10-24T16:00:00Z',
    timeZone: 'America/Bogota',
    responsible: { id: 'u1', name: 'Ana Pérez', isActive: true },
    participants: [],
    notifyTeam: true,
    impact: 'none',
    scope: 'Servidores Windows',
    location: 'Remoto',
    reminderMinutes: 1440,
    tags: [],
    cancelReason: null,
    completion: null,
    outlook: { status: 'not-published', errorCode: null, statusAt: null, webLink: null },
    createdAt: '2026-10-17T12:00:00Z',
    createdBy: 'Luis Gómez',
    updatedAt: null,
    updatedBy: null,
    revision: 1,
    warnings: [],
    outlookEnabled: false,
    ...overrides,
  };
}

export function versionedOf(overrides: Partial<EventDetail> = {}): VersionedEvent {
  const event = detailOf(overrides);
  return { event, etag: `"${event.revision}"` };
}

export function summaryTotals(overrides: Partial<Summary> = {}): Summary {
  return {
    year: 2026,
    months: Array.from({ length: 12 }, (_, index) => ({
      month: index + 1,
      draft: 0,
      scheduled: 0,
      completed: 0,
      cancelled: 0,
      overdue: 0,
    })),
    byType: [],
    ...overrides,
  };
}
