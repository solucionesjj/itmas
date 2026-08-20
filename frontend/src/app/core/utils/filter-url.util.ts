import { ActivatedRoute, Params, Router } from '@angular/router';

/**
 * How one filter control presents itself as an applied-filter chip (design.md §10.1).
 *
 * `label` is always a message key. `valueKey` maps a wire value to a message key
 * for the closed sets (`collaborator` → `category.collaborator`); its absence means
 * the value is free text the user typed, which must be shown verbatim and never
 * run through the catalogue. Making that explicit here is what keeps a template
 * from having to guess which of the two it holds.
 */
export interface FilterMeta {
  readonly label: string;
  readonly valueKey?: (value: string) => string;
}

export type FilterMetaMap = Readonly<Record<string, FilterMeta>>;

/** One chip, fully resolved: nothing left for the template to translate. */
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
function paramsFromFilters(raw: Record<string, string>): Params {
  const params: Params = {};
  for (const [key, value] of Object.entries(raw)) {
    params[key] = value === '' ? undefined : value;
  }
  return params;
}

/**
 * Writes the current filters into the URL, so a filtered view is linkable and
 * survives a refresh.
 *
 * **Never call this while the view is being routed to** — not from a component
 * constructor, `ngOnInit`, or anything else that runs during route activation.
 * Angular instantiates a routed component *inside* the navigation that is bringing
 * it in, before that navigation has emitted `NavigationEnd`. A `router.navigate()`
 * from there supersedes the in-flight navigation, which then ends as
 * `NavigationCancel`; the replacement navigation resolves to the very same URL and
 * so ends as `NavigationSkipped` (`onSameUrlNavigation` defaults to `ignore`).
 * Neither of those is a `NavigationEnd`, and `NavigationEnd` is the only event
 * `RouterLinkActive` and the shell's toolbar title listen for — so the page
 * changed on screen while the sidebar kept highlighting the previous item. The
 * observed event order is:
 *
 *     … ResolveEnd → NavigationCancel → NavigationSkipped → ActivationEnd
 *
 * A filter change is a user action on an already-activated route, which is why
 * that is the only place this is called from.
 *
 * `replaceUrl` so typing in a filter does not stack one history entry per
 * keystroke — the back button should leave the view, not undo a character.
 */
export function syncFiltersToUrl(
  router: Router,
  route: ActivatedRoute,
  raw: Record<string, string>
): void {
  void router.navigate([], {
    relativeTo: route,
    queryParams: paramsFromFilters(raw),
    replaceUrl: true
  });
}

/** True when any filter is set — decides which of §10.4's two empty states applies. */
export function anyFilterActive(raw: Record<string, string>): boolean {
  return Object.values(raw).some((value) => value !== '');
}

/**
 * The set of chips to render above the data (§10.1), with every string already
 * resolved through `translate`. Kept pure — the caller passes the lookup — so it
 * stays testable without a TestBed.
 */
export function describeFilters(
  raw: Record<string, string>,
  meta: FilterMetaMap,
  translate: (key: string) => string
): AppliedFilter[] {
  const applied: AppliedFilter[] = [];
  for (const [key, value] of Object.entries(raw)) {
    if (value === '') {
      continue;
    }
    const entry = meta[key];
    applied.push({
      key,
      label: entry ? translate(entry.label) : key,
      // No `valueKey` means free text the user typed: show it as they wrote it.
      value: entry?.valueKey ? translate(entry.valueKey(value)) : value
    });
  }
  return applied;
}
