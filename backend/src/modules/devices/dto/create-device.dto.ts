import { IsEnum, IsString, MinLength } from 'class-validator';
import { DeviceCategory } from '../device-category.enum';

export class CreateDeviceDto {
  @IsString()
  @MinLength(1)
  hostname!: string;

  @IsEnum(DeviceCategory)
  category!: DeviceCategory;
}
