import { Injectable } from '@nestjs/common';
import { ConfigService } from '@nestjs/config';
import { AlertRulesRepository } from '../alert-rules/alert-rules.repository';
import {
  OffHoursAccessConfig,
  ResourceChangeConfig,
} from '../alert-rules/alert-rule.schema';
import { AlertRuleType } from '../alert-rules/alert-rule-type.enum';
import { AlertsRepository } from '../alerts/alerts.repository';
import { AlertStatus } from '../alerts/alert-status.enum';
import { DeviceCategory } from '../devices/device-category.enum';
import { AccessEventLevel } from '../access-events/access-event-level.enum';
import { AccessEventAction } from '../access-events/access-event-action.enum';
import { InventoryResourceKey } from '../inventories/inventory-diff.service';
import { JsonLoggerService } from '../../common/logger/json-logger.service';
import { isWithinHabitualHours } from './habitual-hours.util';

@Injectable()
export class AlertEngineService {
  constructor(
    private readonly alertRulesRepository: AlertRulesRepository,
    private readonly alertsRepository: AlertsRepository,
    private readonly configService: ConfigService,
    private readonly logger: JsonLoggerService,
  ) {
    this.logger.setContext(AlertEngineService.name);
  }

  /**
   * RF-03/CA-02: raises a `resource_change` alert only for the resources the
   * (single, DB-configured) rule actually enables — never hardcoded here.
   */
  async evaluateResourceChange(
    deviceId: string,
    changes: InventoryResourceKey[],
  ): Promise<void> {
    if (changes.length === 0) {
      return;
    }

    const rule = await this.alertRulesRepository.findEnabledByType(
      AlertRuleType.RESOURCE_CHANGE,
    );
    if (!rule) {
      return;
    }

    const { resources } = rule.config as ResourceChangeConfig;
    // AlertableResource (DTO/schema enum) and InventoryResourceKey (diff
    // service's plain string union) share identical runtime values by design
    // — see alertable-resource.enum.ts.
    const matched = changes.filter((change) =>
      (resources as string[]).includes(change),
    );
    if (matched.length === 0) {
      return;
    }

    await this.alertsRepository.create({
      type: AlertRuleType.RESOURCE_CHANGE,
      deviceId,
      detail: { changes: matched },
      status: AlertStatus.OPEN,
      createdAt: new Date(),
    });
    this.logger.log('Alert raised: resource_change', {
      deviceId,
      changes: matched,
    });
  }

  /**
   * CA-03: raises an `off_hours_access` alert for a login to an
   * infrastructure (server) device outside the configured habitual hours.
   * Collaborator workstations and non-login actions never trigger this rule.
   */
  async evaluateAccessEvent(params: {
    deviceId: string;
    category: DeviceCategory;
    level: AccessEventLevel;
    user: string;
    action: AccessEventAction;
    timestamp: Date;
  }): Promise<void> {
    const { deviceId, category, level, user, action, timestamp } = params;

    if (
      action !== AccessEventAction.LOGIN ||
      category !== DeviceCategory.INFRASTRUCTURE
    ) {
      return;
    }

    const rule = await this.alertRulesRepository.findEnabledByType(
      AlertRuleType.OFF_HOURS_ACCESS,
    );
    if (!rule) {
      return;
    }

    const { habitualHours } = rule.config as OffHoursAccessConfig;
    const timeZone = this.configService.get<string>('habitualHoursTz') ?? 'UTC';

    if (
      isWithinHabitualHours(
        timestamp,
        timeZone,
        habitualHours.from,
        habitualHours.to,
      )
    ) {
      return;
    }

    await this.alertsRepository.create({
      type: AlertRuleType.OFF_HOURS_ACCESS,
      deviceId,
      detail: {
        user,
        level,
        action,
        timestamp: timestamp.toISOString(),
        habitualHours,
      },
      status: AlertStatus.OPEN,
      createdAt: new Date(),
    });
    // Note: don't name this meta field `timestamp` — JsonLoggerService's
    // entry already has its own `timestamp` (the log record's own time) and
    // a same-named meta key would silently overwrite it.
    this.logger.log('Alert raised: off_hours_access', {
      deviceId,
      user,
      eventTimestamp: timestamp.toISOString(),
    });
  }
}
