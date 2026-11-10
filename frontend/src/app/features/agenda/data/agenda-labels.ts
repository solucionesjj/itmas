import { StatusTone } from '../../../shared/models/status-tone';
import { longDate, monthName, parseLocal } from './agenda-time';
import {
  CompletionResult,
  EventStatus,
  Impact,
  OutlookStatus,
  RecurrenceUnit,
  SeriesPattern,
  Warning,
  Weekday,
} from './agenda.models';

/**
 * A state as icon + text (SPEC-0006 RNF-2, §11) on a tinted label of its tone (SPEC-0010 RF-8); the meaning is always carried
 * by the text, never by the colour alone.
 */
export interface StatusPresentation {
  readonly icon: string;
  readonly label: string;
  readonly tone: StatusTone;
}

const STATUS: Record<EventStatus, StatusPresentation> = {
  draft: { icon: 'edit_note', label: 'Borrador', tone: 'neutral' },
  scheduled: { icon: 'event_available', label: 'Programada', tone: 'info' },
  completed: { icon: 'check_circle', label: 'Completada', tone: 'positive' },
  cancelled: { icon: 'cancel', label: 'Cancelada', tone: 'neutral' },
};

const OVERDUE: StatusPresentation = { icon: 'warning', label: 'Vencida', tone: 'risk' };

/** "Vencida" is not a state but a mark (RF-8): it is shown together with the state, not instead of it. */
export function statusPresentation(status: EventStatus): StatusPresentation {
  return STATUS[status];
}

export const overduePresentation = (): StatusPresentation => OVERDUE;

export const statusLabel = (status: EventStatus): string => STATUS[status].label;

const OUTLOOK: Record<OutlookStatus, StatusPresentation> = {
  'not-published': { icon: 'event_busy', label: 'No publicado', tone: 'neutral' },
  pending: { icon: 'sync', label: 'Pendiente', tone: 'warning' },
  published: { icon: 'mark_email_read', label: 'Publicado', tone: 'positive' },
  error: { icon: 'error', label: 'Error', tone: 'risk' },
};

export function outlookPresentation(status: OutlookStatus): StatusPresentation {
  return OUTLOOK[status];
}

/**
 * Spanish labels of the closed code set the API returns for Outlook failures (SPEC-0006 RF-18, SPEC-0008 RF-9). Never a raw
 * detail. `smtp_*` come from the e-mail invitations (ADR-0023), `graph_*` from Microsoft Graph (ADR-0016) and `outlook_*` are
 * the neutral answers of either.
 */
const OUTLOOK_ERRORS: Readonly<Record<string, string>> = {
  smtp_auth_failed: 'El servidor de correo rechazó las credenciales de ITMas.',
  smtp_basic_auth_disabled:
    'Microsoft 365 no permite a ITMas enviar con usuario y contraseña: hay que revisar el envío SMTP de la cuenta.',
  smtp_sender_rejected: 'El servidor de correo no permite enviar desde la cuenta de la agenda.',
  smtp_rejected: 'El servidor de correo rechazó la invitación.',
  smtp_throttled: 'El servidor de correo pidió esperar; inténtalo más tarde.',
  smtp_unavailable: 'No se pudo conectar con el servidor de correo.',
  smtp_timeout: 'El servidor de correo tardó demasiado en responder.',
  outlook_unavailable: 'Outlook no está disponible.',
  outlook_timeout: 'Outlook tardó demasiado en responder.',
  outlook_rejected: 'Outlook rechazó la actividad.',
  graph_unauthorized: 'Outlook rechazó las credenciales de ITMas.',
  graph_forbidden: 'ITMas no tiene permiso sobre el calendario de Outlook.',
  graph_not_found: 'No se encontró el calendario o el evento en Outlook.',
  graph_throttled: 'Outlook pidió esperar: demasiadas solicitudes.',
  graph_unavailable: 'Outlook no está disponible.',
  graph_timeout: 'Outlook tardó demasiado en responder.',
  graph_rejected: 'Outlook rechazó el evento.',
  not_configured: 'La publicación en Outlook no está configurada.',
};

export function outlookErrorText(code: string | null | undefined): string {
  return code ? (OUTLOOK_ERRORS[code] ?? 'No se pudo publicar en Outlook.') : '';
}

const IMPACT: Record<Impact, string> = {
  none: 'Ninguno',
  degradation: 'Degradación',
  outage: 'Indisponibilidad',
};

export const impactLabel = (impact: Impact): string => IMPACT[impact];

export const IMPACTS: readonly Impact[] = ['none', 'degradation', 'outage'];

const RESULT: Record<CompletionResult, string> = {
  succeeded: 'Realizada con éxito',
  partial: 'Realizada parcialmente',
  failed: 'Fallida',
  'not-performed': 'No realizada',
};

export const resultLabel = (result: CompletionResult): string => RESULT[result];

export const RESULTS: readonly CompletionResult[] = [
  'succeeded',
  'partial',
  'failed',
  'not-performed',
];

const UNIT: Record<RecurrenceUnit, { one: string; many: string }> = {
  day: { one: 'día', many: 'días' },
  week: { one: 'semana', many: 'semanas' },
  month: { one: 'mes', many: 'meses' },
  year: { one: 'año', many: 'años' },
};

export const UNITS: readonly RecurrenceUnit[] = ['day', 'week', 'month', 'year'];

export function unitLabel(unit: RecurrenceUnit, interval: number): string {
  return interval === 1 ? UNIT[unit].one : UNIT[unit].many;
}

const WEEKDAY: Record<Weekday, string> = {
  monday: 'lunes',
  tuesday: 'martes',
  wednesday: 'miércoles',
  thursday: 'jueves',
  friday: 'viernes',
  saturday: 'sábado',
  sunday: 'domingo',
};

/** Monday first, as the week of the agenda (P-22). */
export const WEEKDAYS: readonly Weekday[] = [
  'monday',
  'tuesday',
  'wednesday',
  'thursday',
  'friday',
  'saturday',
  'sunday',
];

export const weekdayLabel = (weekday: Weekday): string => WEEKDAY[weekday];

const ORDINAL = ['primer', 'segundo', 'tercer', 'cuarto'];

export const NTHS = [1, 2, 3, 4] as const;

export const ordinalLabel = (nth: number): string => ORDINAL[nth - 1] ?? String(nth);

/**
 * The rule of a series in words, only to describe it (RN-12): "Cada mes, el segundo martes, hasta el 31 de diciembre de 2026".
 * Months and years without a recorded anchor repeat on the day of the start date, so there is nothing more to say about them.
 */
export function ruleLabel(pattern: SeriesPattern): string {
  const unit = unitLabel(pattern.unit, pattern.interval);
  const parts = [pattern.interval === 1 ? `Cada ${unit}` : `Cada ${pattern.interval} ${unit}`];

  const anchor = pattern.anchor;
  if (anchor && (pattern.unit === 'month' || pattern.unit === 'year')) {
    const inMonth =
      pattern.unit === 'year' ? ` de ${monthName(parseLocal(pattern.startsOn).month)}` : '';
    if (anchor.kind === 'nth-weekday' && anchor.nth && anchor.weekday) {
      parts.push(`el ${ordinalLabel(anchor.nth)} ${weekdayLabel(anchor.weekday)}${inMonth}`);
    } else if (anchor.dayOfMonth) {
      parts.push(`el día ${anchor.dayOfMonth}${inMonth}`);
    }
  }
  if (pattern.endsOn) {
    parts.push(`hasta el ${longDate(pattern.endsOn)}`);
  }
  if (pattern.count) {
    parts.push(pattern.count === 1 ? '1 ocurrencia' : `${pattern.count} ocurrencias`);
  }
  return parts.join(', ');
}

/** Message of a warning (§8.4). They never prevent saving. */
export function warningText(warning: Warning): string {
  return warning.kind === 'freeze-overlap'
    ? `Esta actividad se solapa con el congelamiento «${warning.title}».`
    : `Se solapa con «${warning.title}», que también tiene impacto.`;
}

/** "1 día", "2 horas", "90 minutos": a duration expressed in the largest unit that divides it evenly. */
export function durationLabel(minutes: number): string {
  const plural = (count: number, one: string, many: string) =>
    `${count} ${count === 1 ? one : many}`;
  if (minutes > 0 && minutes % 1440 === 0) {
    return plural(minutes / 1440, 'día', 'días');
  }
  if (minutes > 0 && minutes % 60 === 0) {
    return plural(minutes / 60, 'hora', 'horas');
  }
  return plural(minutes, 'minuto', 'minutos');
}

/** "1 día antes", "2 horas antes"… for the reminder of an activity. */
export function reminderLabel(minutes: number): string {
  if (minutes === 0) {
    return 'En el momento del inicio';
  }
  const day = 1440;
  const hour = 60;
  const unit =
    minutes % day === 0
      ? [minutes / day, 'día', 'días']
      : minutes % hour === 0
        ? [minutes / hour, 'hora', 'horas']
        : [minutes, 'minuto', 'minutos'];
  return `${unit[0]} ${unit[0] === 1 ? unit[1] : unit[2]} antes`;
}
