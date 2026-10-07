import { ApiError } from './api-error';
import { problemMessage } from './problem-message';

describe('problemMessage', () => {
  it('prefers the Problem Details detail', () => {
    expect(
      problemMessage(
        new ApiError(409, { title: 'Conflicto', detail: 'Ya existe una política llamada X.' }),
      ),
    ).toBe('Ya existe una política llamada X.');
  });

  it('falls back to the first validation error, then the title', () => {
    expect(
      problemMessage(
        new ApiError(400, { title: 'Inválido', errors: { Name: ['El nombre es obligatorio.'] } }),
      ),
    ).toBe('El nombre es obligatorio.');
    expect(problemMessage(new ApiError(422, { title: 'Regla no satisfecha.' }))).toBe(
      'Regla no satisfecha.',
    );
  });

  it('uses a generic message for unknown errors', () => {
    expect(problemMessage(new Error('boom'))).toBe('No se pudo completar la operación.');
  });
});
