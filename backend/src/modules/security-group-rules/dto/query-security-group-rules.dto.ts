import { Type } from 'class-transformer';
import {
  IsDateString,
  IsEnum,
  IsInt,
  IsOptional,
  IsString,
  Max,
  Min,
} from 'class-validator';
import { SecurityGroupRuleStatus } from '../security-group-rule-status.enum';
import { SecurityGroupRuleSortField } from '../security-group-rule-sort-field.enum';

export enum SortDirection {
  ASC = 'asc',
  DESC = 'desc',
}

export class QuerySecurityGroupRulesDto {
  // Free-text search — matched (regex-escaped) against group/rule name and id.
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

  @IsOptional()
  @IsEnum(SecurityGroupRuleSortField)
  sortBy?: SecurityGroupRuleSortField;

  @IsOptional()
  @IsEnum(SortDirection)
  sortDir?: SortDirection = SortDirection.ASC;

  // Default page size of 20 (agent.md Assumption #9), same pagination shape
  // as QueryAlertsDto/QueryDevicesDto.
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
