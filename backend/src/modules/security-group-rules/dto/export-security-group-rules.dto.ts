import { IsDateString, IsEnum, IsOptional, IsString } from 'class-validator';
import { SecurityGroupRuleStatus } from '../security-group-rule-status.enum';
import { ReportFormat } from '../../reports/report-format.enum';

// Deliberately its own DTO, not QuerySecurityGroupRulesDto reused — export
// has no pagination/sortBy (RF-25: always ordered group-id-then-rule-id)
// and adds `format`, mirroring how QueryReportsDto is separate from
// QueryDevicesDto/QueryAlertsDto rather than bolting export concerns onto
// the listing DTO.
export class ExportSecurityGroupRulesDto {
  @IsEnum(ReportFormat)
  format!: ReportFormat;

  @IsOptional()
  @IsString()
  q?: string;

  @IsOptional()
  @IsString()
  securityGroupId?: string;

  @IsOptional()
  @IsEnum(SecurityGroupRuleStatus)
  status?: SecurityGroupRuleStatus;

  @IsOptional()
  @IsDateString()
  createdFrom?: string;

  @IsOptional()
  @IsDateString()
  createdTo?: string;

  @IsOptional()
  @IsDateString()
  reviewedFrom?: string;

  @IsOptional()
  @IsDateString()
  reviewedTo?: string;

  @IsOptional()
  @IsDateString()
  authorizedFrom?: string;

  @IsOptional()
  @IsDateString()
  authorizedTo?: string;
}
