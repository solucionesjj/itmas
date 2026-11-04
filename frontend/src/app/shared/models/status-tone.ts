/**
 * Tones of the states (SPEC-0010 RF-8, Q-11): equipment, SQL audit and agenda states are tinted labels with an icon or a dot and
 * their text. The theme layer paints them (`.itmas-status`, `.itmas-tone-*`); colour is never the only signal, and no risk or
 * security warning uses the positive or the informative tone.
 */
export type StatusTone = 'positive' | 'warning' | 'risk' | 'info' | 'neutral';

/** Classes of a tinted state label of the given tone (a closed mapping: never built from data). */
export function statusClasses(tone: StatusTone): string {
  return `itmas-status itmas-tone-${tone}`;
}
