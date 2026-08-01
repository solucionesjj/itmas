import { ForbiddenException } from '@nestjs/common';
import { ReportsService } from './reports.service';
import { DevicesRepository } from '../devices/devices.repository';
import { AlertsRepository } from '../alerts/alerts.repository';
import { JsonLoggerService } from '../../common/logger/json-logger.service';
import { UserRole } from '../users/user-role.enum';
import { ReportType } from './report-type.enum';
import { ReportFormat } from './report-format.enum';
import { DeviceCategory } from '../devices/device-category.enum';
import { AlertRuleType } from '../alert-rules/alert-rule-type.enum';
import { AlertStatus } from '../alerts/alert-status.enum';

describe('ReportsService', () => {
  let service: ReportsService;
  let devicesRepository: jest.Mocked<DevicesRepository>;
  let alertsRepository: jest.Mocked<AlertsRepository>;

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

    const logger = {
      setContext: jest.fn(),
      log: jest.fn(),
    } as unknown as JsonLoggerService;

    service = new ReportsService(devicesRepository, alertsRepository, logger);
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
    const result = await service.generate(usuarioUser, {
      reportType: ReportType.DEVICES,
      format: ReportFormat.CSV,
    });
    expect(result.contentType).toBe('text/csv; charset=utf-8');
    expect(result.buffer.toString('utf-8')).toContain('PC-001');
  });

  it('allows Administrador and Auditor to export the alerts report', async () => {
    for (const user of [adminUser, auditorUser]) {
      const result = await service.generate(user, {
        reportType: ReportType.ALERTS,
        format: ReportFormat.CSV,
      });
      expect(result.buffer.toString('utf-8')).toContain('resource_change');
    }
  });

  it('produces a CSV devices report with the expected header row', async () => {
    const result = await service.generate(adminUser, {
      reportType: ReportType.DEVICES,
      format: ReportFormat.CSV,
    });
    const text = result.buffer.toString('utf-8');
    expect(text.split('\r\n')[0]).toBe(
      'hostname,category,os.name,os.version,lastSeen',
    );
    expect(result.filename).toBe('devices-report.csv');
  });

  it('produces a non-trivial PDF buffer', async () => {
    const result = await service.generate(adminUser, {
      reportType: ReportType.DEVICES,
      format: ReportFormat.PDF,
    });
    expect(result.contentType).toBe('application/pdf');
    expect(result.buffer.length).toBeGreaterThan(100);
    // PDF magic bytes.
    expect(result.buffer.subarray(0, 4).toString('ascii')).toBe('%PDF');
  });

  it('flattens alert detail into a readable string for the CSV row', async () => {
    const result = await service.generate(adminUser, {
      reportType: ReportType.ALERTS,
      format: ReportFormat.CSV,
    });
    // The flattened detail contains a `"` (from JSON.stringify'ing the array
    // value), so csv.util quotes the whole field and doubles the internal
    // quotes — assert on content, not the raw unescaped form (that escaping
    // behavior itself is covered by csv.util.spec.ts).
    const text = result.buffer.toString('utf-8');
    expect(text).toContain('changes');
    expect(text).toContain('ram');
  });
});
