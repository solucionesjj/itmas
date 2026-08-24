import {
  BadRequestException,
  ForbiddenException,
  Injectable,
} from '@nestjs/common';
import { DevicesRepository } from '../devices/devices.repository';
import { AlertsRepository } from '../alerts/alerts.repository';
import { SacStatisticsRepository } from '../sac-statistics/sac-statistics.repository';
import { UserRole } from '../users/user-role.enum';
import type { AuthenticatedUser } from '../auth/authenticated-user.interface';
import { JsonLoggerService } from '../../common/logger/json-logger.service';
import { QueryReportsDto } from './dto/query-reports.dto';
import { ReportType } from './report-type.enum';
import { ReportFormat, REPORT_CONTENT_TYPES } from './report-format.enum';
import { ReportFile } from './report-file.interface';
import { CsvCell, toCsv } from './csv.util';
import { writeCsvStream } from './csv-stream.util';
import { buildPdfReport } from './pdf.util';
import { XlsxCell, XlsxColumn, buildXlsxBuffer, writeXlsx } from './xlsx.util';
import {
  SAC_STATISTICS_COLUMNS,
  SAC_STATISTICS_HEADERS,
  toSacCsvRow,
  toSacXlsxRow,
} from './sac-statistics-report.columns';

const DEVICES_HEADERS = [
  'hostname',
  'category',
  'os.name',
  'os.version',
  'lastSeen',
];
const ALERTS_HEADERS = ['type', 'deviceId', 'detail', 'createdAt', 'status'];

/** Bounded reports are plain text columns; only the SAC report needs cell typing. */
const textColumns = (headers: string[]): XlsxColumn[] =>
  headers.map((header) => ({ header, type: 'text' }));

@Injectable()
export class ReportsService {
  constructor(
    private readonly devicesRepository: DevicesRepository,
    private readonly alertsRepository: AlertsRepository,
    private readonly sacStatisticsRepository: SacStatisticsRepository,
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

    this.logger.log('Report exported', {
      actorId: user.sub,
      reportType: query.reportType,
      format: query.format,
    });

    if (query.reportType === ReportType.SAC_STATISTICS) {
      return this.streamSacStatisticsReport(query);
    }

    const { title, headers, rows } =
      query.reportType === ReportType.DEVICES
        ? await this.buildDevicesReport(query)
        : await this.buildAlertsReport(query);

    return {
      kind: 'buffer',
      buffer: await this.serialize(query.format, title, headers, rows),
      contentType: REPORT_CONTENT_TYPES[query.format],
      filename: `${query.reportType}-report.${query.format}`,
    };
  }

  private async serialize(
    format: ReportFormat,
    title: string,
    headers: string[],
    rows: CsvCell[][],
  ): Promise<Buffer> {
    switch (format) {
      case ReportFormat.CSV:
        return Buffer.from(toCsv(headers, rows), 'utf-8');
      case ReportFormat.XLSX:
        return buildXlsxBuffer(
          title,
          textColumns(headers),
          rows as XlsxCell[][],
        );
      case ReportFormat.PDF:
      default:
        return buildPdfReport(title, headers, rows);
    }
  }

  /**
   * BL-032 CA-4/CA-6. Unlike the other two reports this one is streamed: the
   * collection grows daily and keeps its full history by default, so the
   * unfiltered set has no natural ceiling and buffering it would eventually be
   * an out-of-memory export. Rows come off a Mongo cursor in batches, newest
   * first, and are written as they arrive.
   */
  private streamSacStatisticsReport(query: QueryReportsDto): ReportFile {
    if (query.format === ReportFormat.PDF) {
      // Not a silent fallback to another format: the caller asked for something
      // this report cannot produce, and 15 columns genuinely do not fit the PDF
      // generator's fixed layout.
      throw new BadRequestException(
        'The sac-statistics report has 15 columns and does not fit the PDF layout — use format=csv or format=xlsx',
      );
    }

    const filter = {
      databaseName: query.databaseName,
      from: query.from,
      to: query.to,
    };
    const format = query.format;

    return {
      kind: 'stream',
      contentType: REPORT_CONTENT_TYPES[format],
      filename: `sac-statistics-report.${format}`,
      write: async (out) => {
        // A fresh cursor per call — a cursor is single-use, so it cannot be
        // opened before `write` and reused across a retry.
        const cursor = this.sacStatisticsRepository.streamFiltered(filter);
        if (format === ReportFormat.XLSX) {
          await writeXlsx(
            out,
            'SAC',
            SAC_STATISTICS_COLUMNS,
            cursor,
            toSacXlsxRow,
          );
          return;
        }
        await writeCsvStream(out, SAC_STATISTICS_HEADERS, cursor, toSacCsvRow);
      },
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
