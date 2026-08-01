import { DeviceDocument } from './device.schema';
import { DeviceCategory } from './device-category.enum';

export interface DeviceResponse {
  _id: string;
  hostname: string;
  category: DeviceCategory;
  os?: { name: string; version: string };
  lastSeen?: Date;
}

/** Never return apiKeyHash — internal field, not API surface. */
export function toDeviceResponse(device: DeviceDocument): DeviceResponse {
  return {
    _id: device._id,
    hostname: device.hostname,
    category: device.category,
    os: device.os,
    lastSeen: device.lastSeen,
  };
}
