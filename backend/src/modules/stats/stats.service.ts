import { Injectable } from '@nestjs/common';
import {
  DeviceCategoryStats,
  DevicesRepository,
  OsStat,
} from '../devices/devices.repository';

@Injectable()
export class StatsService {
  constructor(private readonly devicesRepository: DevicesRepository) {}

  /** CA-04 */
  getDeviceStats(): Promise<DeviceCategoryStats> {
    return this.devicesRepository.countByCategory();
  }

  /** CA-05 */
  getOsStats(): Promise<OsStat[]> {
    return this.devicesRepository.aggregateByOsName();
  }
}
