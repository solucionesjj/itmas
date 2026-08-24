import { Type } from 'class-transformer';
import {
  IsDateString,
  IsEnum,
  IsInt,
  IsOptional,
  IsString,
  Max,
  MaxLength,
  Min,
} from 'class-validator';
import {
  SacStatisticSortField,
  SacStatisticSortOrder,
} from '../sac-statistic-sort-field.enum';

/**
 * `GET /sac-statistics` (BL-032 CA-1/CA-2). Two filters only — database name and
 * a `generatedAt` range — plus the shared pagination shape used by
 * /devices and /alerts. `sort`/`order` are enums, not free strings, so a caller
 * cannot sort on an unindexed field.
 */
export class QuerySacStatisticsDto {
  /** Partial, case-insensitive; regex-escaped in the repository before use. */
  @IsOptional()
  @IsString()
  @MaxLength(128)
  databaseName?: string;

  @IsOptional()
  @IsDateString()
  from?: string;

  @IsOptional()
  @IsDateString()
  to?: string;

  @IsOptional()
  @IsEnum(SacStatisticSortField)
  sort?: SacStatisticSortField = SacStatisticSortField.GENERATED_AT;

  @IsOptional()
  @IsEnum(SacStatisticSortOrder)
  order?: SacStatisticSortOrder = SacStatisticSortOrder.DESC;

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
