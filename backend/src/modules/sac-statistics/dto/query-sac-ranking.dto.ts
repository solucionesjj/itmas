import { IsDateString, IsEnum, IsOptional } from 'class-validator';
import { SacMetric } from '../sac-metric.enum';

/** `GET /api/v1/stats/sac/ranking` (BL-033 CA-1). */
export class QuerySacRankingDto {
  /**
   * Validated against the nine-value enum (CA-3): the metric name selects a
   * pre-built aggregation expression and is never interpolated into a stage.
   */
  @IsEnum(SacMetric)
  metric!: SacMetric;

  /** As-of date. Omitted means "the most recent snapshot of each database". */
  @IsOptional()
  @IsDateString()
  at?: string;
}
