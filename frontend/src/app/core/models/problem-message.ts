import { ApiError } from './api-error';

/**
 * Client-safe message for an API error, taken from the Problem Details (`detail`, then validation errors, then
 * `title`). Used by features to report validation (400), business-rule (422), conflict (409) and precondition (412) errors.
 */
export function problemMessage(
  error: unknown,
  fallback = 'No se pudo completar la operación.',
): string {
  if (!(error instanceof ApiError) || !error.problem) {
    return fallback;
  }

  const validation = Object.values(error.problem.errors ?? {}).flat();
  return error.problem.detail ?? validation[0] ?? error.problem.title ?? fallback;
}
