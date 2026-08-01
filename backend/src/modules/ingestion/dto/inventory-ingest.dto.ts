import { Type } from 'class-transformer';
import {
  ArrayMinSize,
  IsArray,
  IsDateString,
  IsEnum,
  IsInt,
  IsNumber,
  IsPositive,
  IsString,
  MinLength,
  ValidateNested,
} from 'class-validator';
import { DeviceCategory } from '../../devices/device-category.enum';

export class OsDto {
  @IsString()
  @MinLength(1)
  name!: string;

  @IsString()
  @MinLength(1)
  version!: string;
}

export class CpuDto {
  @IsString()
  @MinLength(1)
  model!: string;

  @IsInt()
  @IsPositive()
  cores!: number;
}

export class RamDto {
  @IsNumber()
  @IsPositive()
  totalGB!: number;
}

export class DiskDto {
  @IsString()
  @MinLength(1)
  name!: string;

  @IsNumber()
  @IsPositive()
  sizeGB!: number;
}

export class InventoryIngestDto {
  @IsString()
  @MinLength(1)
  hostname!: string;

  @IsEnum(DeviceCategory)
  category!: DeviceCategory;

  @ValidateNested()
  @Type(() => OsDto)
  os!: OsDto;

  @ValidateNested()
  @Type(() => CpuDto)
  cpu!: CpuDto;

  @ValidateNested()
  @Type(() => RamDto)
  ram!: RamDto;

  @IsArray()
  @ArrayMinSize(1)
  @ValidateNested({ each: true })
  @Type(() => DiskDto)
  disks!: DiskDto[];

  // Collection time reported by the node, not server receipt time — also
  // the idempotency natural-key half (see inventory.schema.ts).
  @IsDateString()
  timestamp!: string;
}
