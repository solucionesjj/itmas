import { Params } from '@angular/router';

/**
 * How one filter control presents itself as an applied-filter chip (design.md §10.1).
 * `format` turns a wire value into the label a user recognises — `collaborator`
 * into "Colaborador" — so the chip never shows an enum key.
 */
export interface FilterMeta {
  readonly label: string;
  readonly format?: (value: string) => string;
}

export type FilterMetaMap = Readonly<Record<string, FilterMeta>>;

/** One chip: which control it belongs to, and what it reads as. */
export interface AppliedFilter {
  readonly key: string;
  readonly label: string;
  readonly value: string;
}

/**
 * Query params → a patch for the filter form, keeping only the keys the form
 * actually has. Anything else in the URL is left alone: a view must not silently
 * swallow params that belong to someone else, and must not invent controls from
 * whatever a hand-edited URL happens to carry.
 */
export function filtersFromParams(params: Params, keys: readonly string[]): Record<string, string> {
  const patch: Record<string, string> = {};
  for (const key of keys) {
    const value = params[key];
    if (typeof value === 'string' && value !== '') {
      patch[key] = value;
    }
  }
  return patch;
}

/**
 * Filter form → query params, dropping empties so a cleared filter leaves the URL
 * rather than sitting in it as `?hostname=`. `undefined` is what Angular's router
 * uses to remove a param, so cleared keys are explicitly set to it — omitting
 * them would leave the previous value in place.
 */
export function paramsFromFilters(raw: Record<string, string>): Params {
  const params: Params = {};
  for (const [key, value] of Object.entries(raw)) {
    params[key] = value === '' ? undefined : value;
  }
  return params;
}

/** True when any filter is set — decides which of §10.4's two empty states applies. */
export function anyFilterActive(raw: Record<string, string>): boolean {
  return Object.values(raw).some((value) => value !== '');
}

/** The set of chips to render above the data (§10.1). */
export function describeFilters(
  raw: Record<string, string>,
  meta: FilterMetaMap
): AppliedFilter[] {
  const applied: AppliedFilter[] = [];
  for (const [key, value] of Object.entries(raw)) {
    if (value === '') {
      continue;
    }
    const entry = meta[key];
    applied.push({
      key,
      label: entry?.label ?? key,
      value: entry?.format ? entry.format(value) : value
    });
  }
  return applied;
}
