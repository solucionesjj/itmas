import { Injectable, NotFoundException } from '@nestjs/common';
import { AlertsRepository, PagedResult } from './alerts.repository';
import { AlertDocument } from './alert.schema';
import { AlertStatus } from './alert-status.enum';
import { QueryAlertsDto } from './dto/query-alerts.dto';

export interface UpdateAlertStatusResult {
  alert: AlertDocument;
  previousStatus: AlertStatus;
}

@Injectable()
export class AlertsService {
  constructor(private readonly alertsRepository: AlertsRepository) {}

  findAll(query: QueryAlertsDto): Promise<PagedResult<AlertDocument>> {
    return this.alertsRepository.findPaged(
      {
        type: query.type,
        status: query.status,
        from: query.from,
        to: query.to,
      },
      query.page ?? 1,
      query.limit ?? 20,
    );
  }

  async updateStatus(
    id: string,
    status: AlertStatus,
  ): Promise<UpdateAlertStatusResult> {
    const existing = await this.alertsRepository.findById(id);
    if (!existing) {
      throw new NotFoundException(`No alert found with id ${id}`);
    }

    const previousStatus = existing.status;
    const updated = await this.alertsRepository.updateStatus(id, status);
    if (!updated) {
      throw new NotFoundException(`No alert found with id ${id}`);
    }

    return { alert: updated, previousStatus };
  }
}
