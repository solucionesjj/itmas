import { registerLocaleData } from '@angular/common';
import localeEs from '@angular/common/locales/es';
import { LOCALE_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { BytesPipe } from './bytes-pipe';

describe('BytesPipe', () => {
  let pipe: BytesPipe;

  beforeEach(() => {
    registerLocaleData(localeEs);
    TestBed.configureTestingModule({
      providers: [BytesPipe, { provide: LOCALE_ID, useValue: 'es' }],
    });
    pipe = TestBed.inject(BytesPipe);
  });

  it('uses binary units with the Spanish decimal separator', () => {
    expect(pipe.transform(0)).toBe('0 B');
    expect(pipe.transform(512)).toBe('512 B');
    expect(pipe.transform(17179869184)).toBe('16 GB');
    expect(pipe.transform(1610612736)).toBe('1,5 GB');
    expect(pipe.transform(512110190592)).toBe('477 GB');
    expect(pipe.transform(2199023255552)).toBe('2 TB');
  });

  it('does not invent sizes', () => {
    expect(pipe.transform(null)).toBe('No disponible');
    expect(pipe.transform(-1)).toBe('No disponible');
  });
});
