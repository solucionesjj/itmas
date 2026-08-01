import { IsEnum } from 'class-validator';
import { AlertStatus } from '../alert-status.enum';

export class UpdateAlertDto {
  @IsEnum(AlertStatus)
  status!: AlertStatus;
}
