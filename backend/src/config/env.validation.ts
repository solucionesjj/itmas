import { plainToInstance } from 'class-transformer';
import {
  IsInt,
  IsNotEmpty,
  IsOptional,
  IsString,
  Max,
  Min,
  validateSync,
} from 'class-validator';

class EnvironmentVariables {
  @IsString()
  @IsNotEmpty()
  MONGO_URI!: string;

  @IsInt()
  @Min(1)
  @Max(65535)
  PORT!: number;

  @IsString()
  @IsNotEmpty()
  JWT_ACCESS_SECRET!: string;

  @IsString()
  @IsNotEmpty()
  JWT_REFRESH_SECRET!: string;

  @IsString()
  @IsNotEmpty()
  JWT_ACCESS_TTL!: string;

  @IsString()
  @IsNotEmpty()
  JWT_REFRESH_TTL!: string;

  @IsString()
  @IsNotEmpty()
  ADMIN_SEED_USERNAME!: string;

  @IsString()
  @IsNotEmpty()
  ADMIN_SEED_EMAIL!: string;

  @IsString()
  @IsNotEmpty()
  ADMIN_SEED_PASSWORD!: string;

  @IsInt()
  @Min(1)
  LOGIN_RATE_LIMIT_MAX!: number;

  @IsInt()
  @Min(1)
  LOGIN_RATE_LIMIT_WINDOW_SEC!: number;

  @IsOptional()
  @IsInt()
  @Min(1)
  API_RATE_LIMIT_MAX?: number;

  @IsOptional()
  @IsInt()
  @Min(1)
  API_RATE_LIMIT_WINDOW_SEC?: number;

  @IsOptional()
  @IsInt()
  @Min(1)
  INVENTORY_RETENTION_DAYS?: number;

  @IsOptional()
  @IsInt()
  @Min(1)
  ACCESS_EVENTS_RETENTION_DAYS?: number;

  @IsOptional()
  @IsInt()
  @Min(1)
  AUDIT_LOG_RETENTION_DAYS?: number;

  // Optional: defaults to 'UTC' in configuration.ts if unset.
  @IsOptional()
  @IsString()
  HABITUAL_HOURS_TZ?: string;
}

export function validateEnv(config: Record<string, unknown>) {
  const validatedConfig = plainToInstance(EnvironmentVariables, config, {
    enableImplicitConversion: true,
  });
  const errors = validateSync(validatedConfig, {
    skipMissingProperties: false,
  });

  if (errors.length > 0) {
    const messages = errors
      .map((error) => Object.values(error.constraints ?? {}).join(', '))
      .join('; ');
    throw new Error(`Invalid environment configuration: ${messages}`);
  }

  return validatedConfig;
}
