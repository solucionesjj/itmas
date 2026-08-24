import { Transform } from 'class-transformer';
import {
  IsDateString,
  IsInt,
  IsOptional,
  IsString,
  Matches,
  MaxLength,
  Min,
  MinLength,
} from 'class-validator';
import { DECIMAL_18_2, toDecimalString } from '../decimal-18-2.util';

const DECIMAL_MESSAGE =
  'must be an unsigned decimal with at most 18 digits and 2 decimal places, sent as a JSON string';

/**
 * One element of the `POST /sac-statistics` array body (BL-031). Only
 * `databaseName` and `generatedAt` are required; every metric is optional and
 * nullable, exactly like the nullable source columns — a partial row is stored
 * as reported instead of being rejected or zero-filled.
 *
 * `deviceId` is deliberately absent: it comes from the node's API key, so an
 * agent cannot claim to be reporting for another device. The global
 * ValidationPipe runs `whitelist + forbidNonWhitelisted`, so any field not
 * declared here — `deviceId` included — is a 400, not a silent drop.
 */
export class SacStatisticIngestDto {
  @IsString()
  @MinLength(1)
  @MaxLength(128)
  databaseName!: string;

  /** The engine's own generation time, never server-stamped. */
  @IsDateString()
  generatedAt!: string;

  @IsOptional()
  @IsInt()
  @Min(0)
  totalSizeGb?: number | null;

  @IsOptional()
  @IsInt()
  @Min(0)
  debtors?: number | null;

  @IsOptional()
  @IsInt()
  @Min(0)
  debtorActiveAccounts?: number | null;

  @IsOptional()
  @IsInt()
  @Min(0)
  activeAccounts?: number | null;

  @IsOptional()
  @IsInt()
  @Min(0)
  avgActiveUsersLast3Months?: number | null;

  @IsOptional()
  @IsInt()
  @Min(0)
  activities?: number | null;

  @IsOptional()
  @IsInt()
  @Min(0)
  activitiesLast30Days?: number | null;

  @IsOptional()
  @IsInt()
  @Min(0)
  activeUsers?: number | null;

  @IsOptional()
  @IsInt()
  @Min(0)
  logSizeGb?: number | null;

  @IsOptional()
  @IsInt()
  @Min(0)
  dataSizeGb?: number | null;

  // The three numeric(18,2) sums. Declared as strings because that is the only
  // lossless wire form for 18 significant digits; a JSON number is accepted and
  // normalised by the transform, and rejected if it is big enough that the
  // double already lost digits.
  @IsOptional()
  @Transform(({ value }) => toDecimalString(value))
  @Matches(DECIMAL_18_2, { message: `balanceSum ${DECIMAL_MESSAGE}` })
  balanceSum?: string | null;

  @IsOptional()
  @Transform(({ value }) => toDecimalString(value))
  @Matches(DECIMAL_18_2, { message: `overdueSum ${DECIMAL_MESSAGE}` })
  overdueSum?: string | null;

  @IsOptional()
  @Transform(({ value }) => toDecimalString(value))
  @Matches(DECIMAL_18_2, { message: `principalSum ${DECIMAL_MESSAGE}` })
  principalSum?: string | null;
}
