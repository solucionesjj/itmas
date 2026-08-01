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
