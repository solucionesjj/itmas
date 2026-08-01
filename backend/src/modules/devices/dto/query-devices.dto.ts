import { Type } from 'class-transformer';
import { IsEnum, IsInt, IsOptional, IsString, Max, Min } from 'class-validator';
import { DeviceCategory } from '../device-category.enum';

export class QueryDevicesDto {
  @IsOptional()
  @IsEnum(DeviceCategory)
  category?: DeviceCategory;

  @IsOptional()
  @IsString()
  osName?: string;

  @IsOptional()
  @IsString()
  hostname?: string;

  // Default page size of 20 (agent.md Assumption #9: 20-50 range) — same
  // pagination shape as QueryAlertsDto from sub-phase 1.3.
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
