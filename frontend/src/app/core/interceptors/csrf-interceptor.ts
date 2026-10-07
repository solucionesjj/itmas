import { HttpInterceptorFn } from '@angular/common/http';

export const CSRF_HEADER = 'X-CSRF';

/**
 * Adds the anti-CSRF header expected by the API to same-origin requests (relative URLs only).
 * The header is never sent to third-party origins.
 */
export const csrfInterceptor: HttpInterceptorFn = (request, next) => {
  const isSameOrigin = request.url.startsWith('/') && !request.url.startsWith('//');
  return next(isSameOrigin ? request.clone({ setHeaders: { [CSRF_HEADER]: '1' } }) : request);
};
