export type DeviceCategory = 'collaborator' | 'infrastructure';

export interface Device {
  _id: string;
  hostname: string;
  category: DeviceCategory;
  os?: { name: string; version: string };
  lastSeen?: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

export interface DevicesQuery {
  category?: DeviceCategory;
  osName?: string;
  hostname?: string;
  page?: number;
  limit?: number;
}

export interface CreateDeviceRequest {
  hostname: string;
  category: DeviceCategory;
}

// ADR-0016 — the only response shape in the whole API carrying a plaintext
// apiKey; shown once in a reveal dialog and never fetchable again.
export interface CreateDeviceResponse {
  deviceId: string;
  hostname: string;
  category: DeviceCategory;
  apiKey: string;
}

export interface RotateDeviceKeyResponse {
  deviceId: string;
  apiKey: string;
}
