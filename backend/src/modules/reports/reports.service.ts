import { ForbiddenException, Injectable } from '@nestjs/common';
import { DevicesRepository } from '../devices/devices.repository';
import { AlertsRepository } from '../alerts/alerts.repository';
import { UserRole } from '../users/user-role.enum';
import type { AuthenticatedUser } from '../auth/authenticated-user.interface';
import { JsonLoggerService } from '../../common/logger/json-logger.service';
import { QueryReportsDto } from './dto/query-reports.dto';
import { ReportType } from './report-type.enum';
import { ReportFormat } from './report-format.enum';
import { toCsv } from './csv.util';
import { buildPdfReport } from './pdf.util';

export interface ReportFile {
  buffer: Buffer;
  contentType: string;
  filename: string;
}

const DEVICES_HEADERS = [
  'hostname',
  'category',
  'os.name',
  'os.version',
  'lastSeen',
];
const ALERTS_HEADERS = ['type', 'deviceId', 'detail', 'createdAt', 'status'];

@Injectable()
export class ReportsService {
  constructor(
    private readonly devicesRepository: DevicesRepository,
    private readonly alertsRepository: AlertsRepository,
    private readonly logger: JsonLoggerService,
  ) {
    this.logger.setContext(ReportsService.name);
  }

  async generate(
    user: AuthenticatedUser,
    query: QueryReportsDto,
  ): Promise<ReportFile> {
    // Alerts data is Administrador/Auditor-only everywhere else (AlertsController);
    // this endpoint's coarse @Roles() allows Usuario through for the devices
    // report, so the alerts-report path needs its own explicit check here.
    if (query.reportType === ReportType.ALERTS && user.role === UserRole.USER) {
      throw new ForbiddenException(
        'Insufficient role to export the alerts report',
      );
    }

    const { title, headers, rows } =
      query.reportType === ReportType.DEVICES
        ? await this.buildDevicesReport(query)
        : await this.buildAlertsReport(query);

    const buffer =
      query.format === ReportFormat.CSV
        ? Buffer.from(toCsv(headers, rows), 'utf-8')
        : await buildPdfReport(title, headers, rows);

    this.logger.log('Report exported', {
      actorId: user.sub,
      reportType: query.reportType,
      format: query.format,
    });

    return {
      buffer,
      contentType:
        query.format === ReportFormat.CSV
          ? 'text/csv; charset=utf-8'
          : 'application/pdf',
      filename: `${query.reportType}-report.${query.format}`,
    };
  }

  private async buildDevicesReport(query: QueryReportsDto) {
    const devices = await this.devicesRepository.findAllFiltered({
      category: query.category,
      osName: query.osName,
      hostname: query.hostname,
    });

    const rows = devices.map((device) => [
      device.hostname,
      device.category,
      device.os?.name ?? '',
      device.os?.version ?? '',
      device.lastSeen?.toISOString() ?? '',
    ]);

    return { title: 'Reporte de Equipos', headers: DEVICES_HEADERS, rows };
  }

  private async buildAlertsReport(query: QueryReportsDto) {
    const alerts = await this.alertsRepository.findAllFiltered({
      type: query.alertType,
      status: query.status,
      from: query.from,
      to: query.to,
    });

    const rows = alerts.map((alert) => [
      alert.type,
      alert.deviceId,
      this.detailToString(alert.detail),
      alert.createdAt.toISOString(),
      alert.status,
    ]);

    return { title: 'Reporte de Alertas', headers: ALERTS_HEADERS, rows };
  }

  private detailToString(detail?: Record<string, unknown>): string {
    if (!detail) {
      return '';
    }
    return Object.entries(detail)
      .map(([key, value]) => `${key}: ${JSON.stringify(value)}`)
      .join('; ');
  }
}
