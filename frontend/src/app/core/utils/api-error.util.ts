/**
 * What a data view needs to render design.md §10.4's error state: one sentence
 * about what failed, plus the API's correlation id when there is one.
 */
export interface ViewError {
  readonly message: string;
  readonly correlationId?: string;
}

/** The backend's error envelope: `{ error: { code, message, requestId } }`. */
interface ErrorEnvelope {
  error?: {
    message?: unknown;
    requestId?: unknown;
  };
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function asString(value: unknown): string | undefined {
  return typeof value === 'string' && value.length > 0 ? value : undefined;
}

/**
 * Normalises a failed request into a `ViewError`.
 *
 * The backend's envelope is `{ error: { code, message, requestId } }`, and its
 * `requestId` is exactly the correlation id §10.4 asks views to surface so a
 * user can quote it. The server's own message is preferred when present — it is
 * specific ("El CIDR debe incluir la máscara") where a generic fallback is not —
 * and `fallback` covers what never reaches the API at all (offline, DNS, a proxy
 * that is down), where there is no envelope to read.
 *
 * Matched by *shape* rather than `instanceof HttpErrorResponse`: the envelope is
 * the contract that matters, the wrapper class is incidental, and `instanceof`
 * is unreliable across lazy-loaded chunks.
 */
export function toViewError(error: unknown, fallback: string): ViewError {
  if (isRecord(error)) {
    const body = (error as { error?: unknown }).error;
    if (isRecord(body)) {
      const envelope = (body as ErrorEnvelope).error;
      if (isRecord(envelope)) {
        return {
          message: asString(envelope.message) ?? fallback,
          correlationId: asString(envelope.requestId)
        };
      }
    }
  }
  return { message: fallback };
}
