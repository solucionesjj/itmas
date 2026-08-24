import { Type } from 'class-transformer';
import {
  IsEnum,
  IsInt,
  IsOptional,
  IsString,
  Max,
  MaxLength,
  Min,
} from 'class-validator';
import { SacMetric } from '../sac-metric.enum';

/** `GET /api/v1/stats/sac/growth` (BL-033 CA-2). */
export class QuerySacGrowthDto {
  @IsEnum(SacMetric)
  metric!: SacMetric;

  /**
   * Months in the window, counting back from the current month inclusive.
   * Capped at 60: the window becomes a literal array inside the aggregation, and
   * five years is well past the point where a month-by-month chart is readable.
   */
  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(60)
  months?: number = 12;

  /** Partial, case-insensitive; regex-escaped in the repository. */
  @IsOptional()
  @IsString()
  @MaxLength(128)
  databaseName?: string;
}
