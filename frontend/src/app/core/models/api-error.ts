import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from './problem-details';

/** Normalised HTTP error. Feature code inspects `status` and `problem` instead of raw responses. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem: ProblemDetails | null,
  ) {
    super(problem?.title ?? `HTTP ${status}`);
    this.name = 'ApiError';
  }

  static from(response: HttpErrorResponse): ApiError {
    const body: unknown = response.error;
    const problem = body !== null && typeof body === 'object' ? (body as ProblemDetails) : null;
    return new ApiError(response.status, problem);
  }
}
