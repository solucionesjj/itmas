import { Type } from 'class-transformer';
import { IsBoolean, IsEnum, IsOptional, ValidateNested } from 'class-validator';
import { AlertRuleType } from '../alert-rule-type.enum';
import { AlertRuleConfigDto } from './alert-rule-config.dto';

export class CreateAlertRuleDto {
  @IsEnum(AlertRuleType)
  type!: AlertRuleType;

  @IsOptional()
  @IsBoolean()
  enabled?: boolean;

  @ValidateNested()
  @Type(() => AlertRuleConfigDto)
  config!: AlertRuleConfigDto;
}
