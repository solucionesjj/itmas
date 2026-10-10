import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';
import { formatNumber } from '@angular/common';

const UNITS = ['B', 'KB', 'MB', 'GB', 'TB', 'PB'] as const;

/**
 * Human-readable size in binary units (1 KB = 1024 B), as operating systems show memory and disks:
 * `{{ 17179869184 | bytes }}` → "16 GB". Null stays as "No disponible".
 */
@Pipe({ name: 'bytes' })
export class BytesPipe implements PipeTransform {
  private readonly locale = inject(LOCALE_ID);

  transform(value: number | null | undefined): string {
    if (value === null || value === undefined || !Number.isFinite(value) || value < 0) {
      return 'No disponible';
    }

    let size = value;
    let unit = 0;
    while (size >= 1024 && unit < UNITS.length - 1) {
      size /= 1024;
      unit++;
    }
    const digits = unit === 0 || size >= 100 ? '1.0-0' : '1.0-1';
    return `${formatNumber(size, this.locale, digits)} ${UNITS[unit]}`;
  }
}
