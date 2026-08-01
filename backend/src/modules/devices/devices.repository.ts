import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { Device, DeviceDocument } from './device.schema';
import { DeviceCategory } from './device-category.enum';
import { escapeRegex } from '../../common/util/escape-regex.util';

export interface DevicesFilter {
  category?: DeviceCategory;
  osName?: string;
  hostname?: string;
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  limit: number;
}

export interface DeviceCategoryStats {
  total: number;
  collaborator: number;
  infrastructure: number;
}

export interface OsStat {
  os: string;
  count: number;
}

const UNKNOWN_OS_LABEL = 'unknown';

@Injectable()
export class DevicesRepository {
  constructor(
    @InjectModel(Device.name)
    private readonly deviceModel: Model<DeviceDocument>,
  ) {}

  findById(deviceId: string): Promise<DeviceDocument | null> {
    return this.deviceModel.findById(deviceId).exec();
  }

  /**
   * Shared by `findPaged` (portal listing) and `findAllFiltered` (report
   * export) — only ever populated from validated, whitelisted DTO fields
   * (QueryDevicesDto), never raw client-supplied keys, to avoid NoSQL
   * operator injection (agent.md §6.7). `osName`/`hostname` are escaped
   * before use in a $regex (ReDoS-safe).
   */
  private buildQuery(filter: DevicesFilter): Record<string, unknown> {
    const query: Record<string, unknown> = {};
    if (filter.category) {
      query.category = filter.category;
    }
    if (filter.osName) {
      query['os.name'] = { $regex: escapeRegex(filter.osName), $options: 'i' };
    }
    if (filter.hostname) {
      query.hostname = { $regex: escapeRegex(filter.hostname), $options: 'i' };
    }
    return query;
  }

  async findPaged(
    filter: DevicesFilter,
    page: number,
    limit: number,
  ): Promise<PagedResult<DeviceDocument>> {
    const query = this.buildQuery(filter);

    const [items, total] = await Promise.all([
      this.deviceModel
        .find(query)
        .sort({ lastSeen: -1 })
        .skip((page - 1) * limit)
        .limit(limit)
        .exec(),
      this.deviceModel.countDocuments(query).exec(),
    ]);

    return { items, total, page, limit };
  }

  /** Full filtered set, no pagination — used by report export (sub-phase 1.6). */
  findAllFiltered(filter: DevicesFilter): Promise<DeviceDocument[]> {
    return this.deviceModel
      .find(this.buildQuery(filter))
      .sort({ lastSeen: -1 })
      .exec();
  }

  /** CA-04: device counts by category. */
  async countByCategory(): Promise<DeviceCategoryStats> {
    const [collaborator, infrastructure, total] = await Promise.all([
      this.deviceModel
        .countDocuments({ category: DeviceCategory.COLLABORATOR })
        .exec(),
      this.deviceModel
        .countDocuments({ category: DeviceCategory.INFRASTRUCTURE })
        .exec(),
      this.deviceModel.countDocuments({}).exec(),
    ]);
    return { total, collaborator, infrastructure };
  }

  /**
   * CA-05: device counts grouped by os.name. Devices that haven't sent an
   * inventory yet (os unset — touchOnIngest only sets it after first
   * ingestion) are bucketed as "unknown" rather than silently dropped, so
   * the sum of counts always equals the total device count.
   */
  async aggregateByOsName(): Promise<OsStat[]> {
    const results = await this.deviceModel
      .aggregate<{ _id: string; count: number }>([
        {
          $group: {
            _id: { $ifNull: ['$os.name', UNKNOWN_OS_LABEL] },
            count: { $sum: 1 },
          },
        },
        { $sort: { count: -1 } },
      ])
      .exec();

    return results.map((r) => ({ os: r._id, count: r.count }));
  }

  create(data: Device): Promise<DeviceDocument> {
    return this.deviceModel.create(data);
  }

  async setApiKeyHash(deviceId: string, apiKeyHash: string): Promise<void> {
    await this.deviceModel
      .updateOne({ _id: deviceId }, { $set: { apiKeyHash } })
      .exec();
  }

  async touchOnIngest(
    deviceId: string,
    data: {
      hostname: string;
      category: DeviceCategory;
      os: { name: string; version: string };
    },
  ): Promise<void> {
    await this.deviceModel
      .updateOne(
        { _id: deviceId },
        {
          $set: {
            hostname: data.hostname,
            category: data.category,
            os: data.os,
            lastSeen: new Date(),
          },
        },
      )
      .exec();
  }

  async touchLastSeen(deviceId: string): Promise<void> {
    await this.deviceModel
      .updateOne({ _id: deviceId }, { $set: { lastSeen: new Date() } })
      .exec();
  }
}
