import { PassThrough } from 'stream';
import { BadRequestException, ForbiddenException } from '@nestjs/common';
import { ReportsService } from './reports.service';
import { DevicesRepository } from '../devices/devices.repository';
import { AlertsRepository } from '../alerts/alerts.repository';
import { SacStatisticsRepository } from '../sac-statistics/sac-statistics.repository';
import { JsonLoggerService } from '../../common/logger/json-logger.service';
import { ReportFile } from './report-file.interface';
import { UserRole } from '../users/user-role.enum';
import { ReportType } from './report-type.enum';
import { ReportFormat } from './report-format.enum';
import { DeviceCategory } from '../devices/device-category.enum';
import { AlertRuleType } from '../alert-rules/alert-rule-type.enum';
import { AlertStatus } from '../alerts/alert-status.enum';

/**
 * Narrows a ReportFile to its buffered variant. The bounded reports
 * (devices/alerts) are always buffered; only sac-statistics streams, and it has
 * its own assertions below.
 */
function expectBuffer(file: ReportFile) {
  if (file.kind !== 'buffer') {
    throw new Error(`expected a buffered report, got ${file.kind}`);
  }
  return file;
}

/** Drains a streamed report into memory so a test can assert on its bytes. */
async function collect(file: ReportFile): Promise<Buffer> {
  if (file.kind !== 'stream') {
    throw new Error(`expected a streamed report, got ${file.kind}`);
  }
  const sink = new PassThrough();
  const chunks: Buffer[] = [];
  sink.on('data', (chunk: Buffer) => chunks.push(chunk));
  await file.write(sink);
  return Buffer.concat(chunks);
}

/**
 * Async-iterable stand-in for the Mongo cursor the repository hands over.
 * Built from a sync iterator rather than an `async function*` because nothing
 * here actually awaits — the point is only that the consumer sees an
 * AsyncIterable, which is all `writeXlsx`/`writeCsvStream` require.
 */
function cursorOf<T>(rows: T[]): AsyncIterable<T> {
  return {
    [Symbol.asyncIterator]() {
      let index = 0;
      return {
        next: () =>
          Promise.resolve(
            index < rows.length
              ? { value: rows[index++], done: false }
              : { value: undefined as never, done: true },
          ),
      };
    },
  };
}

describe('ReportsService', () => {
  let service: ReportsService;
  let devicesRepository: jest.Mocked<DevicesRepository>;
  let alertsRepository: jest.Mocked<AlertsRepository>;
  let sacStatisticsRepository: jest.Mocked<SacStatisticsRepository>;

  /**
   * Held as its own jest.fn rather than reached through the mock object, so
   * assertions reference a plain function instead of an unbound method.
   */
  let streamFiltered: jest.Mock;

  const adminUser = {
    sub: 'admin-1',
    username: 'admin',
    role: UserRole.ADMINISTRATOR,
    mustChangePassword: false,
  };
  const usuarioUser = {
    sub: 'user-1',
    username: 'jperez',
    role: UserRole.USER,
    mustChangePassword: false,
  };
  const auditorUser = {
    sub: 'auditor-1',
    username: 'auditor1',
    role: UserRole.AUDITOR,
    mustChangePassword: false,
  };

  beforeEach(() => {
    devicesRepository = {
      findAllFiltered: jest.fn().mockResolvedValue([
        {
          hostname: 'PC-001',
          category: DeviceCategory.COLLABORATOR,
          os: { name: 'Windows', version: '11' },
          lastSeen: new Date('2026-01-01T10:00:00.000Z'),
        },
      ]),
    } as unknown as jest.Mocked<DevicesRepository>;

    alertsRepository = {
      findAllFiltered: jest.fn().mockResolvedValue([
        {
          type: AlertRuleType.RESOURCE_CHANGE,
          deviceId: 'device-1',
          detail: { changes: ['ram'] },
          createdAt: new Date('2026-01-01T10:00:00.000Z'),
          status: AlertStatus.OPEN,
        },
      ]),
    } as unknown as jest.Mocked<AlertsRepository>;

    streamFiltered = jest.fn(() =>
      cursorOf([
        {
          databaseName: 'DBSAC_Acme',
          generatedAt: new Date('2026-03-15T04:00:00.000Z'),
          totalSizeGb: 512,
          dataSizeGb: 500,
          logSizeGb: 12,
          debtors: 1200,
          debtorActiveAccounts: 1500,
          activeAccounts: 1800,
          balanceSum: { toString: () => '1234.56' },
          overdueSum: { toString: () => '10.00' },
          principalSum: { toString: () => '20.50' },
          activities: 987654,
          activitiesLast30Days: 12345,
          activeUsers: 45,
          avgActiveUsersLast3Months: 42,
        },
      ]),
    );
    sacStatisticsRepository = {
      streamFiltered,
    } as unknown as jest.Mocked<SacStatisticsRepository>;

    const logger = {
      setContext: jest.fn(),
      log: jest.fn(),
    } as unknown as JsonLoggerService;

    service = new ReportsService(
      devicesRepository,
      alertsRepository,
      sacStatisticsRepository,
      logger,
    );
  });

  it('rejects a Usuario exporting the alerts report', async () => {
    await expect(
      service.generate(usuarioUser, {
        reportType: ReportType.ALERTS,
        format: ReportFormat.CSV,
      }),
    ).rejects.toBeInstanceOf(ForbiddenException);
  });

  it('allows a Usuario to export the devices report', async () => {
    const result = expectBuffer(
      await service.generate(usuarioUser, {
        reportType: ReportType.DEVICES,
        format: ReportFormat.CSV,
      }),
    );
    expect(result.contentType).toBe('text/csv; charset=utf-8');
    expect(result.buffer.toString('utf-8')).toContain('PC-001');
  });

  it('allows Administrador and Auditor to export the alerts report', async () => {
    for (const user of [adminUser, auditorUser]) {
      const result = expectBuffer(
        await service.generate(user, {
          reportType: ReportType.ALERTS,
          format: ReportFormat.CSV,
        }),
      );
      expect(result.buffer.toString('utf-8')).toContain('resource_change');
    }
  });

  it('produces a CSV devices report with the expected header row', async () => {
    const result = expectBuffer(
      await service.generate(adminUser, {
        reportType: ReportType.DEVICES,
        format: ReportFormat.CSV,
      }),
    );
    const text = result.buffer.toString('utf-8');
    expect(text.split('\r\n')[0]).toBe(
      'hostname,category,os.name,os.version,lastSeen',
    );
    expect(result.filename).toBe('devices-report.csv');
  });

  it('produces a non-trivial PDF buffer', async () => {
    const result = expectBuffer(
      await service.generate(adminUser, {
        reportType: ReportType.DEVICES,
        format: ReportFormat.PDF,
      }),
    );
    expect(result.contentType).toBe('application/pdf');
    expect(result.buffer.length).toBeGreaterThan(100);
    // PDF magic bytes.
    expect(result.buffer.subarray(0, 4).toString('ascii')).toBe('%PDF');
  });

  it('flattens alert detail into a readable string for the CSV row', async () => {
    const result = expectBuffer(
      await service.generate(adminUser, {
        reportType: ReportType.ALERTS,
        format: ReportFormat.CSV,
      }),
    );
    // The flattened detail contains a `"` (from JSON.stringify'ing the array
    // value), so csv.util quotes the whole field and doubles the internal
    // quotes — assert on content, not the raw unescaped form (that escaping
    // behavior itself is covered by csv.util.spec.ts).
    const text = result.buffer.toString('utf-8');
    expect(text).toContain('changes');
    expect(text).toContain('ram');
  });

  // BL-032 CA-4/CA-5/CA-6.
  describe('sac-statistics report', () => {
    it('streams rather than buffers — the collection has no natural ceiling', async () => {
      const file = await service.generate(usuarioUser, {
        reportType: ReportType.SAC_STATISTICS,
        format: ReportFormat.CSV,
      });

      expect(file.kind).toBe('stream');
      expect(file.filename).toBe('sac-statistics-report.csv');
    });

    it('opens the cursor only when the body is written, since a cursor is single-use', async () => {
      const file = await service.generate(adminUser, {
        reportType: ReportType.SAC_STATISTICS,
        format: ReportFormat.CSV,
      });

      expect(streamFiltered).not.toHaveBeenCalled();
      await collect(file);
      expect(streamFiltered).toHaveBeenCalledTimes(1);
    });

    it('emits the 15 contract field names as the CSV header row', async () => {
      const csv = (
        await collect(
          await service.generate(adminUser, {
            reportType: ReportType.SAC_STATISTICS,
            format: ReportFormat.CSV,
          }),
        )
      ).toString('utf-8');

      const headers = csv.split('\r\n')[0].split(',');
      expect(headers).toHaveLength(15);
      expect(headers[0]).toBe('databaseName');
      expect(headers).toContain('avgActiveUsersLast3Months');
      // Provenance bookkeeping, not source data — must not be in the report.
      expect(headers).not.toContain('deviceId');
    });

    it('keeps the financial sums as exact decimal strings in CSV', async () => {
      const csv = (
        await collect(
          await service.generate(adminUser, {
            reportType: ReportType.SAC_STATISTICS,
            format: ReportFormat.CSV,
          }),
        )
      ).toString('utf-8');

      expect(csv).toContain('1234.56');
    });

    it('passes the query filters through to the cursor', async () => {
      await collect(
        await service.generate(adminUser, {
          reportType: ReportType.SAC_STATISTICS,
          format: ReportFormat.CSV,
          databaseName: 'acme',
          from: '2026-01-01T00:00:00.000Z',
          to: '2026-06-30T00:00:00.000Z',
        }),
      );

      expect(streamFiltered).toHaveBeenCalledWith({
        databaseName: 'acme',
        from: '2026-01-01T00:00:00.000Z',
        to: '2026-06-30T00:00:00.000Z',
      });
    });

    it('produces a real xlsx package (ZIP magic bytes) for format=xlsx', async () => {
      const buffer = await collect(
        await service.generate(adminUser, {
          reportType: ReportType.SAC_STATISTICS,
          format: ReportFormat.XLSX,
        }),
      );

      // An .xlsx is a ZIP container — 'PK\x03\x04'.
      expect(buffer.subarray(0, 2).toString('ascii')).toBe('PK');
      expect(buffer.length).toBeGreaterThan(100);
    });

    it('sets the xlsx content type and filename', async () => {
      const file = await service.generate(adminUser, {
        reportType: ReportType.SAC_STATISTICS,
        format: ReportFormat.XLSX,
      });

      expect(file.contentType).toBe(
        'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
      );
      expect(file.filename).toBe('sac-statistics-report.xlsx');
    });

    it('refuses format=pdf with a 400 rather than silently substituting a format', async () => {
      await expect(
        service.generate(adminUser, {
          reportType: ReportType.SAC_STATISTICS,
          format: ReportFormat.PDF,
        }),
      ).rejects.toBeInstanceOf(BadRequestException);
    });

    it('is open to a Usuario — these are general consultation data', async () => {
      await expect(
        service.generate(usuarioUser, {
          reportType: ReportType.SAC_STATISTICS,
          format: ReportFormat.XLSX,
        }),
      ).resolves.toMatchObject({ kind: 'stream' });
    });
  });

  // BL-032 CA-4: the xlsx serialiser is generic, so it works for the
  // pre-existing report types without their data paths changing.
  describe('xlsx for the pre-existing report types', () => {
    it('produces an xlsx devices report', async () => {
      const result = expectBuffer(
        await service.generate(adminUser, {
          reportType: ReportType.DEVICES,
          format: ReportFormat.XLSX,
        }),
      );

      expect(result.buffer.subarray(0, 2).toString('ascii')).toBe('PK');
      expect(result.filename).toBe('devices-report.xlsx');
    });

    it('produces an xlsx alerts report', async () => {
      const result = expectBuffer(
        await service.generate(auditorUser, {
          reportType: ReportType.ALERTS,
          format: ReportFormat.XLSX,
        }),
      );

      expect(result.buffer.subarray(0, 2).toString('ascii')).toBe('PK');
      expect(result.filename).toBe('alerts-report.xlsx');
    });
  });
});
