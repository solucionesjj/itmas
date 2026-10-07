/** RFC 9457 Problem Details, as returned by the API for every error. */
export interface ProblemDetails {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly instance?: string;
  readonly traceId?: string;
  readonly errors?: Record<string, string[]>;
}
