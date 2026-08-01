import { Type } from 'class-transformer';
import {
  ArrayMinSize,
  IsArray,
  IsEnum,
  IsOptional,
  ValidateNested,
} from 'class-validator';
import { AlertableResource } from '../alertable-resource.enum';
import { HabitualHoursDto } from './habitual-hours.dto';

// Both fields are structurally optional here — which one is actually
// required depends on the rule's `type` (resource_change → resources,
// off_hours_access → habitualHours). AlertRulesService enforces that
// cross-field rule; class-validator alone can't express it cleanly across
// a parent DTO's sibling property.
export class AlertRuleConfigDto {
  @IsOptional()
  @IsArray()
  @ArrayMinSize(1)
  @IsEnum(AlertableResource, { each: true })
  resources?: AlertableResource[];

  @IsOptional()
  @ValidateNested()
  @Type(() => HabitualHoursDto)
  habitualHours?: HabitualHoursDto;
}
