import {
  BadRequestException,
  ConflictException,
  Injectable,
  NotFoundException,
  OnModuleInit,
} from '@nestjs/common';
import { Types } from 'mongoose';
import { AlertRulesRepository } from './alert-rules.repository';
import { AlertRuleDocument, AlertRuleConfig } from './alert-rule.schema';
import { AlertRuleType } from './alert-rule-type.enum';
import { AlertableResource } from './alertable-resource.enum';
import { CreateAlertRuleDto } from './dto/create-alert-rule.dto';
import { UpdateAlertRuleDto } from './dto/update-alert-rule.dto';
import { AlertRuleConfigDto } from './dto/alert-rule-config.dto';
import { JsonLoggerService } from '../../common/logger/json-logger.service';

// agent.md Assumption #7 defaults — seeded once on first boot, then owned by
// whoever administers the rule via the REST endpoints (the engine always
// reads from the DB, this is only the out-of-the-box starting point).
const DEFAULT_RESOURCE_CHANGE_CONFIG: AlertRuleConfig = {
  resources: [
    AlertableResource.CPU,
    AlertableResource.RAM,
    AlertableResource.DISKS,
  ],
};
const DEFAULT_OFF_HOURS_CONFIG: AlertRuleConfig = {
  habitualHours: { from: '07:00', to: '19:00' },
};

@Injectable()
export class AlertRulesService implements OnModuleInit {
  constructor(
    private readonly alertRulesRepository: AlertRulesRepository,
    private readonly logger: JsonLoggerService,
  ) {
    this.logger.setContext(AlertRulesService.name);
  }

  async onModuleInit(): Promise<void> {
    const seeds: Array<[AlertRuleType, AlertRuleConfig]> = [
      [AlertRuleType.RESOURCE_CHANGE, DEFAULT_RESOURCE_CHANGE_CONFIG],
      [AlertRuleType.OFF_HOURS_ACCESS, DEFAULT_OFF_HOURS_CONFIG],
    ];

    for (const [type, config] of seeds) {
      const { created } = await this.alertRulesRepository.createIfMissing({
        type,
        enabled: true,
        config,
        updatedAt: new Date(),
      });
      if (created) {
        this.logger.log('Seeded default alert rule', { type });
      }
    }
  }

  findAll(): Promise<AlertRuleDocument[]> {
    return this.alertRulesRepository.findAll();
  }

  async create(
    dto: CreateAlertRuleDto,
    createdBy?: string,
  ): Promise<AlertRuleDocument> {
    this.assertConfigMatchesType(dto.type, dto.config);

    const { created, rule } = await this.alertRulesRepository.createIfMissing({
      type: dto.type,
      enabled: dto.enabled ?? true,
      config: dto.config as AlertRuleConfig,
      createdBy: createdBy ? new Types.ObjectId(createdBy) : undefined,
      updatedAt: new Date(),
    });

    if (!created || !rule) {
      throw new ConflictException(
        `A rule of type "${dto.type}" already exists — use PATCH to modify it.`,
      );
    }

    return rule;
  }

  async update(
    id: string,
    dto: UpdateAlertRuleDto,
  ): Promise<AlertRuleDocument> {
    const existing = await this.alertRulesRepository.findById(id);
    if (!existing) {
      throw new NotFoundException(`No alert rule found with id ${id}`);
    }

    if (dto.config) {
      this.assertConfigMatchesType(existing.type, dto.config);
    }

    const updated = await this.alertRulesRepository.update(id, {
      enabled: dto.enabled,
      config: dto.config as AlertRuleConfig | undefined,
    });

    // update() can only return null here if the document was deleted between
    // the findById above and this call — vanishingly unlikely, but handled.
    if (!updated) {
      throw new NotFoundException(`No alert rule found with id ${id}`);
    }

    return updated;
  }

  private assertConfigMatchesType(
    type: AlertRuleType,
    config: AlertRuleConfigDto,
  ): void {
    if (type === AlertRuleType.RESOURCE_CHANGE && !config.resources) {
      throw new BadRequestException(
        'config.resources is required for a resource_change rule',
      );
    }

    if (type === AlertRuleType.OFF_HOURS_ACCESS && !config.habitualHours) {
      throw new BadRequestException(
        'config.habitualHours is required for an off_hours_access rule',
      );
    }
  }
}
