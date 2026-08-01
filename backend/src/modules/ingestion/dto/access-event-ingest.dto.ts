import { IsDateString, IsEnum, IsString, MinLength } from 'class-validator';
import { AccessEventLevel } from '../../access-events/access-event-level.enum';
import { AccessEventAction } from '../../access-events/access-event-action.enum';

export class AccessEventIngestDto {
  @IsEnum(AccessEventLevel)
  level!: AccessEventLevel;

  @IsString()
  @MinLength(1)
  user!: string;

  // Event time reported by the node — also the idempotency natural-key half.
  @IsDateString()
  timestamp!: string;

  @IsEnum(AccessEventAction)
  action!: AccessEventAction;
}
