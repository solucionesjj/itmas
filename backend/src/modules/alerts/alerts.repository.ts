import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { Alert, AlertDocument } from './alert.schema';
import { AlertRuleType } from '../alert-rules/alert-rule-type.enum';
import { AlertStatus } from './alert-status.enum';

export interface AlertsFilter {
  type?: AlertRuleType;
  status?: AlertStatus;
  from?: string;
  to?: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

@Injectable()
export class AlertsRepository {
  constructor(
    @InjectModel(Alert.name) private readonly alertModel: Model<AlertDocument>,
  ) {}

  create(data: Alert): Promise<AlertDocument> {
    return this.alertModel.create(data);
  }

  findById(id: string): Promise<AlertDocument | null> {
    return this.alertModel.findById(id).exec();
  }

  /**
   * Shared by `findPaged` (portal listing) and `findAllFiltered` (report
   * export) — only ever populated from validated, whitelisted DTO fields
   * (see QueryAlertsDto) — never raw client-supplied keys, to avoid NoSQL
   * operator injection (agent.md §6.7).
   */
  private buildQuery(filter: AlertsFilter): Record<string, unknown> {
    const query: Record<string, unknown> = {};
    if (filter.type) query.type = filter.type;
    if (filter.status) query.status = filter.status;
    if (filter.from || filter.to) {
      const createdAt: Record<string, Date> = {};
      if (filter.from) createdAt.$gte = new Date(filter.from);
      if (filter.to) createdAt.$lte = new Date(filter.to);
      query.createdAt = createdAt;
    }
    return query;
  }

  async findPaged(
    filter: AlertsFilter,
    page: number,
    limit: number,
  ): Promise<PagedResult<AlertDocument>> {
    const query = this.buildQuery(filter);

    const [items, total] = await Promise.all([
      this.alertModel
        .find(query)
        .sort({ createdAt: -1 })
        .skip((page - 1) * limit)
        .limit(limit)
        .exec(),
      this.alertModel.countDocuments(query).exec(),
    ]);

    return { items, total, page, limit };
  }

  /** Full filtered set, no pagination — used by report export (sub-phase 1.6). */
  findAllFiltered(filter: AlertsFilter): Promise<AlertDocument[]> {
    return this.alertModel
      .find(this.buildQuery(filter))
      .sort({ createdAt: -1 })
      .exec();
  }

  async updateStatus(
    id: string,
    status: AlertStatus,
  ): Promise<AlertDocument | null> {
    return this.alertModel
      .findByIdAndUpdate(id, { $set: { status } }, { returnDocument: 'after' })
      .exec();
  }
}
