import { DeviceCategory } from '../devices/device.model';
import { AlertStatus } from '../alerts/alert.model';

export type ReportType = 'devices' | 'alerts';
// `xlsx` arrived with the SAC statistics report (BL-032), but the serialiser is
// generic, so it applies to these two report types as well.
export type ReportFormat = 'csv' | 'pdf' | 'xlsx';

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
