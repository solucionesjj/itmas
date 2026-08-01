import { DeviceCategory } from '../devices/device.model';
import { AlertStatus } from '../alerts/alert.model';

export type ReportType = 'devices' | 'alerts';
export type ReportFormat = 'csv' | 'pdf';

export interface ReportQuery {
  type: ReportType;
  format: ReportFormat;
  category?: DeviceCategory;
  osName?: string;
  hostname?: string;
  status?: AlertStatus;
  from?: string;
  to?: string;
}
