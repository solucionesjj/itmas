import { IsString, Matches } from 'class-validator';

const HH_MM_PATTERN = /^([01]\d|2[0-3]):[0-5]\d$/;

export class HabitualHoursDto {
  @IsString()
  @Matches(HH_MM_PATTERN, { message: 'from must be an "HH:mm" time (24h)' })
  from!: string;

  @IsString()
  @Matches(HH_MM_PATTERN, { message: 'to must be an "HH:mm" time (24h)' })
  to!: string;
}
