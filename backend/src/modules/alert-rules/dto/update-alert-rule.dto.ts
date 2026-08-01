import { Type } from 'class-transformer';
import { IsBoolean, IsOptional, ValidateNested } from 'class-validator';
import { AlertRuleConfigDto } from './alert-rule-config.dto';

export class UpdateAlertRuleDto {
  @IsOptional()
  @IsBoolean()
  enabled?: boolean;

  @IsOptional()
  @ValidateNested()
  @Type(() => AlertRuleConfigDto)
  config?: AlertRuleConfigDto;
}
