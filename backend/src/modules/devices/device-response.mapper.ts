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

/**
 * POST /devices (ADR-0016) — the ONLY response shape in this whole API that
 * ever carries a plaintext `apiKey`. It is returned exactly once, at
 * creation time; every subsequent read of this device (e.g. GET /devices)
 * goes through `toDeviceResponse` above, which never includes it.
 */
export interface CreateDeviceResponse {
  deviceId: string;
  hostname: string;
  category: DeviceCategory;
  apiKey: string;
}

/**
 * POST /devices/:id/rotate-key (ADR-0016) — same one-time-reveal property as
 * `CreateDeviceResponse`: the new plaintext `apiKey` is returned exactly
 * once here and never again.
 */
export interface RotateDeviceKeyResponse {
  deviceId: string;
  apiKey: string;
}
