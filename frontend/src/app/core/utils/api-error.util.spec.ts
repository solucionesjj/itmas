import { HttpErrorResponse } from '@angular/common/http';
import { toViewError } from './api-error.util';

describe('toViewError', () => {
  const FALLBACK = 'No se pudieron cargar los datos.';

  it("prefers the server's own message over the fallback", () => {
    const error = { error: { error: { code: 'BAD_REQUEST', message: 'CIDR inválido' } } };

    expect(toViewError(error, FALLBACK).message).toBe('CIDR inválido');
  });

  it('surfaces requestId as the correlation id', () => {
    const error = { error: { error: { message: 'boom', requestId: 'abc-123' } } };

    expect(toViewError(error, FALLBACK).correlationId).toBe('abc-123');
  });

  it('reads a real HttpErrorResponse too', () => {
    const error = new HttpErrorResponse({
      status: 500,
      error: { error: { message: 'Fallo interno', requestId: 'req-9' } }
    });

    const result = toViewError(error, FALLBACK);
    expect(result.message).toBe('Fallo interno');
    expect(result.correlationId).toBe('req-9');
  });

  // Matching by shape rather than `instanceof HttpErrorResponse` is deliberate:
  // the envelope is the contract, and `instanceof` is unreliable across
  // lazy-loaded chunks. A plain object carrying the envelope must still parse.
  it('does not depend on the error being an HttpErrorResponse instance', () => {
    const plain = { error: { error: { message: 'igual de válido' } } };

    expect(toViewError(plain, FALLBACK).message).toBe('igual de válido');
  });

  describe('falls back when there is no envelope to read', () => {
    const cases: [string, unknown][] = [
      ['a network failure with no body', new HttpErrorResponse({ status: 0, error: null })],
      ['a plain Error', new Error('offline')],
      ['null', null],
      ['undefined', undefined],
      ['a string', 'kaboom'],
      ['an empty envelope', { error: { error: {} } }],
      ['a blank message', { error: { error: { message: '' } } }]
    ];

    for (const [label, value] of cases) {
      it(label, () => {
        // Asserted property by property: the fallback path omits `correlationId`
        // entirely, and Jasmine's toEqual treats a missing key as different from
        // one explicitly set to undefined.
        const result = toViewError(value, FALLBACK);
        expect(result.message).toBe(FALLBACK);
        expect(result.correlationId).toBeUndefined();
      });
    }
  });

  it('keeps the correlation id even when only the message is missing', () => {
    const error = { error: { error: { requestId: 'solo-id' } } };

    const result = toViewError(error, FALLBACK);
    expect(result.message).toBe(FALLBACK);
    expect(result.correlationId).toBe('solo-id');
  });
});
