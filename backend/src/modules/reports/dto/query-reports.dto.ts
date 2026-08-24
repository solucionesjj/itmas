import {
  IsDateString,
  IsEnum,
  IsOptional,
  IsString,
  MaxLength,
} from 'class-validator';
import { DeviceCategory } from '../../devices/device-category.enum';
import { AlertRuleType } from '../../alert-rules/alert-rule-type.enum';
import { AlertStatus } from '../../alerts/alert-status.enum';
import { ReportType } from '../report-type.enum';
import { ReportFormat } from '../report-format.enum';

// `reportType` names the report's own selector (devices|alerts|sac-statistics)
// so it never collides with `alertType` below — the alert domain's own `type`
// field (resource_change|off_hours_access), reused here as an optional filter.
export class QueryReportsDto {
  @IsEnum(ReportType)
  reportType!: ReportType;

  @IsEnum(ReportFormat)
  format!: ReportFormat;

  // devices-report filters (ignored when reportType=alerts)
  @IsOptional()
  @IsEnum(DeviceCategory)
  category?: DeviceCategory;

  @IsOptional()
  @IsString()
  osName?: string;

  @IsOptional()
  @IsString()
  hostname?: string;

  // sac-statistics-report filter (ignored by the other types). The `from`/`to`
  // range below is shared with the alerts report — there it bounds `createdAt`,
  // here `generatedAt`, in both cases "the report's own time axis".
  @IsOptional()
  @IsString()
  @MaxLength(128)
  databaseName?: string;

  // alerts-report filters (ignored when reportType=devices)
  @IsOptional()
  @IsEnum(AlertRuleType)
  alertType?: AlertRuleType;

  @IsOptional()
  @IsEnum(AlertStatus)
  status?: AlertStatus;

  @IsOptional()
  @IsDateString()
  from?: string;

  @IsOptional()
  @IsDateString()
  to?: string;
}
