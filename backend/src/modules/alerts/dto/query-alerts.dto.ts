import { Type } from 'class-transformer';
import {
  IsDateString,
  IsEnum,
  IsInt,
  IsOptional,
  Max,
  Min,
} from 'class-validator';
import { AlertRuleType } from '../../alert-rules/alert-rule-type.enum';
import { AlertStatus } from '../alert-status.enum';

export class QueryAlertsDto {
  @IsOptional()
  @IsEnum(AlertRuleType)
  type?: AlertRuleType;

  @IsOptional()
  @IsEnum(AlertStatus)
  status?: AlertStatus;

  @IsOptional()
  @IsDateString()
  from?: string;

  @IsOptional()
  @IsDateString()
  to?: string;

  // Default page size of 20 (agent.md Assumption #9: 20-50 range).
  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  page?: number = 1;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(100)
  limit?: number = 20;
}
