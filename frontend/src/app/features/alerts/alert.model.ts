export type AlertType = 'resource_change' | 'off_hours_access';
export type AlertStatus = 'open' | 'reviewed';

export interface Alert {
  _id: string;
  type: AlertType;
  deviceId: string;
  detail: Record<string, unknown>;
  createdAt: string;
  status: AlertStatus;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

export interface AlertsQuery {
  type?: AlertType;
  status?: AlertStatus;
  from?: string;
  to?: string;
  page?: number;
  limit?: number;
}
