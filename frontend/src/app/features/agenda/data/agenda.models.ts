/**
 * Contracts of the infrastructure agenda API (SPEC-0006 §12.1). Everything a person typed (titles, descriptions, tags,
 * scope, notes, links) is untrusted: render it as text, never as HTML.
 *
 * Dates are local to the organization zone (RNF-9): date-times (`2026-10-24T09:00:00`) for timed events and dates
 * (`2026-10-26`) for all-day ones, where `end` is the last day, inclusive (P-24).
 */

export type EventStatus = 'draft' | 'scheduled' | 'completed' | 'cancelled';
export type StatusFilter = EventStatus | 'overdue';
export type Impact = 'none' | 'degradation' | 'outage';
export type OutlookStatus = 'not-published' | 'pending' | 'published' | 'error';
export type CompletionResult = 'succeeded' | 'partial' | 'failed' | 'not-performed';
export type RecurrenceUnit = 'day' | 'week' | 'month' | 'year';
export type AnchorKind = 'day-of-month' | 'nth-weekday';
export type Weekday =
  'monday' | 'tuesday' | 'wednesday' | 'thursday' | 'friday' | 'saturday' | 'sunday';
export type WarningKind = 'freeze-overlap' | 'impact-overlap';

export interface TypeRef {
  readonly id: string;
  readonly name: string;
  readonly isFreeze: boolean;
}

export interface Person {
  readonly id: string;
  readonly name: string;
  readonly isActive: boolean;
}

/** A person that can be chosen as the responsible (P-31). */
export interface PersonOption {
  readonly id: string;
  readonly name: string;
  readonly isMe: boolean;
}

export interface Warning {
  readonly kind: WarningKind;
  readonly eventId: string;
  readonly title: string;
}

export interface OutlookState {
  readonly status: OutlookStatus;
  readonly errorCode?: string | null;
}

export interface EventSummary {
  readonly id: string;
  readonly seriesId: string | null;
  readonly type: TypeRef;
  readonly title: string;
  readonly start: string;
  readonly end: string;
  readonly startUtc: string;
  readonly endUtc: string;
  readonly isAllDay: boolean;
  readonly status: EventStatus;
  readonly isOverdue: boolean;
  readonly responsible: Person;
  readonly impact: Impact;
  readonly tags: readonly string[];
  readonly hasWarnings: boolean;
  readonly outlook: { readonly status: OutlookStatus };
}

export interface EventList {
  readonly items: readonly EventSummary[];
  /** More events existed than the 1 000 returned (§8.4). */
  readonly truncated: boolean;
  readonly timeZone: string;
  /** Whether scheduling publishes to Outlook (§8.4). */
  readonly outlookEnabled: boolean;
}

export interface MonthSummary {
  readonly month: number;
  readonly draft: number;
  readonly scheduled: number;
  readonly completed: number;
  readonly cancelled: number;
  /** How many of the drafts and scheduled ones have passed their end without being closed. */
  readonly overdue: number;
}

export interface Summary {
  readonly year: number;
  readonly months: readonly MonthSummary[];
  readonly byType: readonly { readonly typeId: string; readonly count: number }[];
}

export interface TagCount {
  readonly name: string;
  readonly count: number;
}

export interface EventType {
  readonly id: string;
  readonly name: string;
  readonly description: string | null;
  readonly defaultDurationMinutes: number;
  readonly defaultReminderMinutes: number;
  readonly isFreeze: boolean;
  readonly isActive: boolean;
  readonly revision: number;
}

export interface EvidenceLink {
  readonly label: string;
  readonly url: string;
}

export interface Completion {
  readonly result: CompletionResult;
  readonly executedAt: string;
  readonly completedAt: string;
  readonly completedBy: string | null;
  readonly notes: string | null;
  readonly evidenceLinks: readonly EvidenceLink[];
}

export interface OutlookDetail {
  readonly status: OutlookStatus;
  readonly errorCode: string | null;
  readonly statusAt: string | null;
  readonly webLink: string | null;
}

export interface SeriesAnchor {
  readonly kind: AnchorKind;
  readonly dayOfMonth: number | null;
  readonly nth: number | null;
  readonly weekday: Weekday | null;
}

export interface SeriesPattern {
  readonly unit: RecurrenceUnit;
  readonly interval: number;
  readonly anchor: SeriesAnchor | null;
  readonly startsOn: string;
  readonly endsOn: string | null;
  readonly count: number | null;
}

export interface EventDetail {
  readonly id: string;
  readonly seriesId: string | null;
  readonly series: SeriesPattern | null;
  readonly type: TypeRef;
  readonly title: string;
  readonly description: string | null;
  readonly status: EventStatus;
  readonly isOverdue: boolean;
  readonly isAllDay: boolean;
  readonly start: string;
  readonly end: string;
  readonly startUtc: string;
  readonly endUtc: string;
  readonly timeZone: string;
  readonly responsible: Person;
  readonly participants: readonly string[];
  readonly notifyTeam: boolean;
  readonly impact: Impact;
  readonly scope: string | null;
  readonly location: string | null;
  readonly reminderMinutes: number;
  readonly tags: readonly string[];
  readonly cancelReason: string | null;
  readonly completion: Completion | null;
  readonly outlook: OutlookDetail;
  readonly createdAt: string;
  readonly createdBy: string | null;
  readonly updatedAt: string | null;
  readonly updatedBy: string | null;
  readonly revision: number;
  readonly warnings: readonly Warning[];
  readonly outlookEnabled: boolean;
}

/** An event read together with the ETag needed to update it (optimistic concurrency, RNF-3). */
export interface VersionedEvent {
  readonly event: EventDetail;
  readonly etag: string;
}

/** Body of POST and PUT `/agenda-events` (RF-3). `end` may be omitted: the type gives the default duration. */
export interface EventInput {
  readonly typeId: string;
  readonly title: string;
  readonly description: string | null;
  readonly responsibleUserId: string;
  readonly participants: readonly string[];
  readonly notifyTeam: boolean;
  readonly impact: Impact;
  readonly scope: string | null;
  readonly location: string | null;
  readonly reminderMinutes: number | null;
  readonly tags: readonly string[];
  readonly isAllDay: boolean;
  readonly start: string;
  readonly end: string | null;
}

export interface SeriesRuleInput {
  readonly unit: RecurrenceUnit;
  readonly interval: number;
  readonly anchor: {
    readonly kind: AnchorKind;
    readonly dayOfMonth?: number | null;
    readonly nth?: number | null;
    readonly weekday?: Weekday | null;
  } | null;
  readonly endsOn: string | null;
  readonly count: number | null;
}

export interface SeriesInput {
  readonly event: EventInput;
  readonly rule: SeriesRuleInput;
}

export interface SeriesCommonInput {
  readonly typeId: string;
  readonly title: string;
  readonly description: string | null;
  readonly responsibleUserId: string;
  readonly participants: readonly string[];
  readonly impact: Impact;
  readonly scope: string | null;
  readonly location: string | null;
  readonly reminderMinutes: number | null;
  readonly tags: readonly string[] | null;
}

export interface SeriesOccurrence {
  readonly start: string;
  readonly end: string;
  readonly isAllDay: boolean;
  readonly warnings: readonly Warning[];
}

export interface SeriesCreated {
  readonly seriesId: string;
  readonly eventIds: readonly string[];
}

export interface ScheduleResult {
  readonly id: string;
  readonly status: EventStatus | null;
  readonly warnings: readonly Warning[];
  readonly outlook: OutlookState;
  readonly error: string | null;
}

export interface CompletionInput {
  readonly result: CompletionResult;
  readonly executedAt: string | null;
  readonly notes: string | null;
  readonly evidenceLinks: readonly EvidenceLink[];
}

export interface EventTypeInput {
  readonly name: string;
  readonly description: string | null;
  readonly defaultDurationMinutes: number;
  readonly defaultReminderMinutes: number;
  readonly isFreeze: boolean;
  readonly isActive?: boolean;
}

/** Maximum figures of the contract the forms mirror to guide the person early (the API enforces them: 422). */
export const LIMITS = {
  title: 120,
  description: 4000,
  scope: 500,
  location: 200,
  reason: 500,
  notes: 4000,
  tag: 30,
  tags: 10,
  participants: 30,
  evidenceLinks: 10,
  evidenceLabel: 100,
  evidenceUrl: 500,
  maxOccurrences: 60,
  maxReminderMinutes: 40_320,
  maxDurationMinutes: 86_400,
  typeName: 80,
  typeDescription: 500,
  batch: 50,
} as const;
